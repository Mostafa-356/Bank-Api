using System.Text.Json;
using Bank.Application.DTOs;
using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

/// <summary>
/// Service for managing account lockouts and failed login attempts
/// </summary>
public sealed class AccountLockoutService : IAccountLockoutService
{
    private readonly IAccountLockoutRepository _accountLockoutRepository;
    private readonly IPasswordPolicyService _passwordPolicyService;
    private readonly IAuditEventPublisher _auditEventPublisher;
    private readonly ILogger<AccountLockoutService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IUnitOfWork _unitOfWork;

    // Configuration settings
    private readonly int _defaultMaxFailedAttempts;
    private readonly TimeSpan _defaultLockoutDuration;
    private readonly bool _enableProgressiveLockout;

    public AccountLockoutService(
        IAccountLockoutRepository accountLockoutRepository,
        IPasswordPolicyService passwordPolicyService,
        IAuditEventPublisher auditEventPublisher,
        ILogger<AccountLockoutService> logger,
        IConfiguration configuration,
        IUnitOfWork unitOfWork)
    {
        _accountLockoutRepository = accountLockoutRepository;
        _passwordPolicyService = passwordPolicyService;
        _auditEventPublisher = auditEventPublisher;
        _logger = logger;
        _configuration = configuration;
        _unitOfWork = unitOfWork;

        // Load configuration settings
        _defaultMaxFailedAttempts = int.Parse(_configuration["Security:Lockout:MaxFailedAttempts"] ?? "5");
        _defaultLockoutDuration = TimeSpan.FromMinutes(int.Parse(_configuration["Security:Lockout:DurationMinutes"] ?? "30"));
        _enableProgressiveLockout = bool.Parse(_configuration["Security:Lockout:EnableProgressive"] ?? "true");
    }

    public async Task<LockoutResult> RecordFailedLoginAttemptAsync(Guid userId, string? ipAddress = null, string? userAgent = null)
    {
        try
        {
            var lockout = await GetOrCreateLockoutRecordAsync(userId);
            
            // Check if account is already locked
            if (lockout.IsCurrentlyLocked && !lockout.IsLockoutExpired())
            {
                return new LockoutResult
                {
                    IsLocked = true,
                    FailedAttempts = lockout.FailedAttempts,
                    MaxFailedAttempts = await GetMaxFailedAttemptsForUserAsync(userId),
                    RemainingLockoutTime = lockout.GetRemainingLockoutTime(),
                    LockoutReason = lockout.LockoutReason,
                    Message = "Account is currently locked due to too many failed login attempts"
                };
            }

            // Auto-unlock if lockout has expired
            if (lockout.IsLockoutExpired())
            {
                lockout.UnlockAccount();
                _accountLockoutRepository.Update(lockout);
                await _unitOfWork.SaveChangesAsync();
            }

            // Record the failed attempt
            lockout.RecordFailedAttempt(ipAddress, userAgent);
            
            var maxFailedAttempts = await GetMaxFailedAttemptsForUserAsync(userId);
            var shouldLock = lockout.ShouldLockAccount(maxFailedAttempts);

            if (shouldLock)
            {
                // Calculate lockout duration (progressive lockout)
                var lockoutDuration = await CalculateLockoutDurationAsync(userId, lockout.FailedAttempts);
                
                lockout.LockAccount(AccountLockoutReason.FailedLoginAttempts, lockoutDuration);

                // Publish security event for account lockout
                await _auditEventPublisher.PublishSecurityEventAsync(
                    userId,
                    "AccountLockedFailedAttempts",
                    "User",
                    userId.ToString(),
                    ipAddress,
                    userAgent,
                    JsonSerializer.Serialize(new 
                    { 
                        FailedAttempts = lockout.FailedAttempts,
                        LockoutDuration = lockoutDuration.TotalMinutes,
                        LockoutReason = AccountLockoutReason.FailedLoginAttempts.ToString()
                    }));

                _logger.LogWarning("Account {UserId} locked after {FailedAttempts} failed login attempts from IP {IpAddress}", 
                    userId, lockout.FailedAttempts, ipAddress);
            }
            else
            {
                // Publish security event for failed login attempt
                await _auditEventPublisher.PublishSecurityEventAsync(
                    userId,
                    "FailedLoginAttempt",
                    "User",
                    userId.ToString(),
                    ipAddress,
                    userAgent,
                    JsonSerializer.Serialize(new { FailedAttempts = lockout.FailedAttempts }));

                _logger.LogWarning("Failed login attempt for user {UserId} from IP {IpAddress}. Attempt {FailedAttempts}/{MaxAttempts}", 
                    userId, ipAddress, lockout.FailedAttempts, maxFailedAttempts);
            }

            _accountLockoutRepository.Update(lockout);
            await _unitOfWork.SaveChangesAsync();

            return new LockoutResult
            {
                IsLocked = shouldLock,
                FailedAttempts = lockout.FailedAttempts,
                MaxFailedAttempts = maxFailedAttempts,
                RemainingLockoutTime = shouldLock ? lockout.GetRemainingLockoutTime() : null,
                LockoutReason = shouldLock ? AccountLockoutReason.FailedLoginAttempts : null,
                ShouldLockAccount = shouldLock,
                Message = shouldLock 
                    ? $"Account locked for {lockout.GetRemainingLockoutTime()?.TotalMinutes:F0} minutes due to too many failed login attempts"
                    : $"Failed login attempt recorded. {maxFailedAttempts - lockout.FailedAttempts} attempts remaining"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording failed login attempt for user {UserId}", userId);
            return new LockoutResult
            {
                IsLocked = false,
                Message = "Error processing login attempt"
            };
        }
    }

    public async Task RecordSuccessfulLoginAsync(Guid userId)
    {
        try
        {
            var lockout = await _accountLockoutRepository.GetByUserIdAsync(userId);
            if (lockout != null)
            {
                var wasLocked = lockout.IsCurrentlyLocked;
                lockout.RecordSuccessfulLogin();
                _accountLockoutRepository.Update(lockout);
                await _unitOfWork.SaveChangesAsync();

                if (wasLocked)
                {
                    // Publish security event for account unlock
                    await _auditEventPublisher.PublishSecurityEventAsync(
                        userId,
                        "AccountUnlockedSuccessfulLogin",
                        "User",
                        userId.ToString(),
                        additionalData: JsonSerializer.Serialize(new { UnlockReason = "Successful login after lockout expiry" }));

                    _logger.LogInformation("Account {UserId} unlocked after successful login", userId);
                }

                // Publish security event for successful login
                await _auditEventPublisher.PublishSecurityEventAsync(
                    userId,
                    "SuccessfulLogin",
                    "User",
                    userId.ToString());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording successful login for user {UserId}", userId);
        }
    }

    public async Task<bool> IsAccountLockedAsync(Guid userId)
    {
        try
        {
            var lockout = await _accountLockoutRepository.GetByUserIdAsync(userId);
            if (lockout == null)
                return false;

            // Check if lockout has expired
            if (lockout.IsLockoutExpired())
            {
                lockout.UnlockAccount();
                _accountLockoutRepository.Update(lockout);
                await _unitOfWork.SaveChangesAsync();
                return false;
            }

            return lockout.IsCurrentlyLocked;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if account {UserId} is locked", userId);
            return false;
        }
    }

    public async Task<AccountLockout?> GetLockoutStatusAsync(Guid userId)
    {
        try
        {
            return await _accountLockoutRepository.GetByUserIdAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lockout status for user {UserId}", userId);
            return null;
        }
    }

    public async Task<bool> LockAccountAsync(Guid userId, AccountLockoutReason reason, TimeSpan? lockoutDuration = null, string? notes = null, Guid? lockedByUserId = null)
    {
        try
        {
            var lockout = await GetOrCreateLockoutRecordAsync(userId);
            var duration = lockoutDuration ?? _defaultLockoutDuration;

            if (lockoutDuration == null && reason == AccountLockoutReason.AdminAction)
            {
                // Permanent lockout for admin actions unless duration specified
                lockout.LockAccountPermanently(reason, notes, lockedByUserId);
            }
            else
            {
                lockout.LockAccount(reason, duration, notes, lockedByUserId);
            }

            _accountLockoutRepository.Update(lockout);
            await _unitOfWork.SaveChangesAsync();

            // Publish security event
            await _auditEventPublisher.PublishSecurityEventAsync(
                lockedByUserId ?? userId,
                "AccountLockedManually",
                "User",
                userId.ToString(),
                additionalData: JsonSerializer.Serialize(new 
                { 
                    LockoutReason = reason.ToString(),
                    LockoutDuration = lockoutDuration?.TotalMinutes,
                    Notes = notes,
                    LockedByUserId = lockedByUserId
                }));

            _logger.LogWarning("Account {UserId} manually locked. Reason: {Reason}, Duration: {Duration}, Locked by: {LockedBy}", 
                userId, Bank.Application.Services.SecureLoggingService.SanitizeInput(reason.ToString()), lockoutDuration, lockedByUserId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error locking account {UserId}", userId);
            return false;
        }
    }

    public async Task<bool> UnlockAccountAsync(Guid userId, Guid? unlockedByUserId = null)
    {
        try
        {
            var lockout = await _accountLockoutRepository.GetByUserIdAsync(userId);
            if (lockout == null || !lockout.IsCurrentlyLocked)
                return false;

            lockout.UnlockAccount(unlockedByUserId);
            _accountLockoutRepository.Update(lockout);
            await _unitOfWork.SaveChangesAsync();

            // Publish security event
            await _auditEventPublisher.PublishSecurityEventAsync(
                unlockedByUserId ?? userId,
                "AccountUnlockedManually",
                "User",
                userId.ToString(),
                additionalData: JsonSerializer.Serialize(new { UnlockedByUserId = unlockedByUserId }));

            _logger.LogInformation("Account {UserId} manually unlocked by user {UnlockedBy}", 
                userId, unlockedByUserId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unlocking account {UserId}", userId);
            return false;
        }
    }

    public async Task<TimeSpan?> GetRemainingLockoutTimeAsync(Guid userId)
    {
        try
        {
            var lockout = await _accountLockoutRepository.GetByUserIdAsync(userId);
            return lockout?.GetRemainingLockoutTime();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting remaining lockout time for user {UserId}", userId);
            return null;
        }
    }

    public async Task<int> GetFailedAttemptCountAsync(Guid userId)
    {
        try
        {
            var lockout = await _accountLockoutRepository.GetByUserIdAsync(userId);
            return lockout?.FailedAttempts ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting failed attempt count for user {UserId}", userId);
            return 0;
        }
    }

    public async Task CleanupExpiredLockoutsAsync()
    {
        try
        {
            var expiredLockouts = await _accountLockoutRepository.GetExpiredLockoutsAsync();
            var cleanedUpCount = 0;

            foreach (var lockout in expiredLockouts)
            {
                lockout.UnlockAccount();
                _accountLockoutRepository.Update(lockout);
                cleanedUpCount++;

                // Publish system event - using security event since there's no system event method
                await _auditEventPublisher.PublishSecurityEventAsync(
                    null,
                    "LockoutExpired",
                    "AccountLockout",
                    lockout.Id.ToString(),
                    additionalData: JsonSerializer.Serialize(new { UserId = lockout.UserId }));
            }
            
            if (cleanedUpCount > 0)
            {
                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation("Cleaned up {Count} expired account lockouts", cleanedUpCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired lockouts");
        }
    }

    public async Task<LockoutStatistics> GetLockoutStatisticsAsync()
    {
        try
        {
            var allLockouts = await _accountLockoutRepository.GetAllLockoutsAsync();
            var lockedAccounts = allLockouts.Where(l => l.IsCurrentlyLocked).ToList();
            var today = DateTime.UtcNow.Date;

            return new LockoutStatistics
            {
                TotalLockedAccounts = lockedAccounts.Count,
                AccountsLockedToday = allLockouts.Count(l => l.CreatedAt.Date == today && l.IsCurrentlyLocked),
                AccountsUnlockedToday = allLockouts.Count(l => l.CreatedAt.Date == today && !l.IsCurrentlyLocked),
                LockoutsByReason = lockedAccounts.Where(l => l.LockoutReason.HasValue)
                    .GroupBy(l => l.LockoutReason!.Value)
                    .ToDictionary(g => g.Key, g => g.Count()),
                LastCleanupAt = DateTime.UtcNow // This would be tracked separately in production
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lockout statistics");
            return new LockoutStatistics();
        }
    }

    public async Task<List<AccountLockout>> GetLockedAccountsAsync()
    {
        try
        {
            return await _accountLockoutRepository.GetCurrentlyLockedAccountsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving locked accounts");
            return new List<AccountLockout>();
        }
    }

    public async Task<List<AccountLockout>> GetLockoutHistoryAsync(Guid userId)
    {
        try
        {
            return await _accountLockoutRepository.GetLockoutHistoryAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving lockout history for user {UserId}", userId);
            return new List<AccountLockout>();
        }
    }

    public async Task<bool> ShouldLockAccountAsync(Guid userId)
    {
        try
        {
            var lockout = await _accountLockoutRepository.GetByUserIdAsync(userId);
            if (lockout == null)
                return false;

            var maxFailedAttempts = await GetMaxFailedAttemptsForUserAsync(userId);
            return lockout.ShouldLockAccount(maxFailedAttempts);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if account {UserId} should be locked", userId);
            return false;
        }
    }

    private async Task<AccountLockout> GetOrCreateLockoutRecordAsync(Guid userId)
    {
        var lockout = await _accountLockoutRepository.GetByUserIdAsync(userId);
        if (lockout == null)
        {
            lockout = new AccountLockout(userId);
            await _accountLockoutRepository.AddAsync(lockout);
            await _unitOfWork.SaveChangesAsync();
        }
        return lockout;
    }

    private async Task<int> GetMaxFailedAttemptsForUserAsync(Guid userId)
    {
        try
        {
            var policy = await _passwordPolicyService.GetDefaultPasswordPolicyAsync();
            return policy?.MaxFailedAttempts ?? _defaultMaxFailedAttempts;
        }
        catch
        {
            return _defaultMaxFailedAttempts;
        }
    }

    private async Task<TimeSpan> CalculateLockoutDurationAsync(Guid userId, int failedAttempts)
    {
        try
        {
            var policy = await _passwordPolicyService.GetDefaultPasswordPolicyAsync();
            var baseDuration = policy?.LockoutDuration ?? _defaultLockoutDuration;

            if (!_enableProgressiveLockout)
                return baseDuration;

            // Progressive lockout: increase duration based on number of failed attempts
            var multiplier = Math.Min(failedAttempts / 5, 4); // Cap at 4x the base duration
            return TimeSpan.FromMilliseconds(baseDuration.TotalMilliseconds * Math.Pow(2, multiplier));
        }
        catch
        {
            return _defaultLockoutDuration;
        }
    }
}