using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Bank.Application.DTOs;
using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

/// <summary>
/// Service for managing IP whitelist for administrative and secure access
/// </summary>
public sealed class IpWhitelistService : IIpWhitelistService
{
    private readonly IIpWhitelistRepository _ipWhitelistRepository;
    private readonly IAuditEventPublisher _auditEventPublisher;
    private readonly ILogger<IpWhitelistService> _logger;
    private readonly IUnitOfWork _unitOfWork;

    public IpWhitelistService(
        IIpWhitelistRepository ipWhitelistRepository,
        IAuditEventPublisher auditEventPublisher,
        ILogger<IpWhitelistService> logger,
        IUnitOfWork unitOfWork)
    {
        _ipWhitelistRepository = ipWhitelistRepository;
        _auditEventPublisher = auditEventPublisher;
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task<IpWhitelistResult> AddIpToWhitelistAsync(string ipAddress, IpWhitelistType type, string description, Guid createdByUserId, string? ipRange = null, DateTime? expiresAt = null)
    {
        try
        {
            // Validate IP address format
            if (!ValidateIpAddress(ipAddress, ipRange))
            {
                return new IpWhitelistResult
                { Success = false,
                    ErrorMessage = "Invalid IP address or range format"
                };
            }

            // Check if IP already exists for this type
            var existingEntry = await _ipWhitelistRepository.GetByIpAddressAndTypeAsync(ipAddress, type);
            if (existingEntry != null)
            {
                return new IpWhitelistResult
                { Success = false,
                    ErrorMessage = "IP address already exists in whitelist for this access type"
                };
            }

            // Create new whitelist entry
            var whitelistEntry = new IpWhitelist(ipAddress, type, description, createdByUserId, ipRange, expiresAt);
            await _ipWhitelistRepository.AddAsync(whitelistEntry);
            await _unitOfWork.SaveChangesAsync();

            // Publish audit event
            await _auditEventPublisher.PublishSecurityEventAsync(
                createdByUserId,
                "IpWhitelistEntryCreated",
                "IpWhitelist",
                whitelistEntry.Id.ToString(),
                additionalData: JsonSerializer.Serialize(new 
                { 
                    IpAddress = ipAddress, 
                    Type = type.ToString(), 
                    Description = description,
                    IpRange = ipRange,
                    ExpiresAt = expiresAt
                }));

            _logger.LogInformation("IP whitelist entry created for {IpAddress} ({Type}) by user {UserId}", 
                SecureLoggingService.SanitizeInput(ipAddress), type, createdByUserId);

            return new IpWhitelistResult
            { Success = true,
                WhitelistId = whitelistEntry.Id,
                RequiresApproval = true // All entries require approval by default
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding IP {IpAddress} to whitelist", SecureLoggingService.SanitizeInput(ipAddress));
            return new IpWhitelistResult
            { Success = false,
                ErrorMessage = "Failed to add IP to whitelist"
            };
        }
    }

    public async Task<bool> RemoveIpFromWhitelistAsync(Guid whitelistId)
    {
        try
        {
            var entry = await _ipWhitelistRepository.GetByIdAsync(whitelistId);
            if (entry == null)
                return false;

            _ipWhitelistRepository.Remove(entry);
            await _unitOfWork.SaveChangesAsync();

            // Publish audit event
            await _auditEventPublisher.PublishSecurityEventAsync(
                null,
                "IpWhitelistEntryRemoved",
                "IpWhitelist",
                whitelistId.ToString(),
                additionalData: JsonSerializer.Serialize(new 
                { 
                    IpAddress = entry.IpAddress, 
                    Type = entry.Type.ToString() 
                }));

            _logger.LogInformation("IP whitelist entry removed for {IpAddress} ({Type})", 
                entry.IpAddress, entry.Type);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing IP whitelist entry {WhitelistId}", whitelistId);
            return false;
        }
    }

    public async Task<bool> ApproveIpWhitelistAsync(Guid whitelistId, Guid approvedByUserId, string? notes = null)
    {
        try
        {
            var entry = await _ipWhitelistRepository.GetByIdAsync(whitelistId);
            if (entry == null)
                return false;

            entry.Approve(approvedByUserId, notes);
            _ipWhitelistRepository.Update(entry);
            await _unitOfWork.SaveChangesAsync();

            // Publish audit event
            await _auditEventPublisher.PublishSecurityEventAsync(
                approvedByUserId,
                "IpWhitelistEntryApproved",
                "IpWhitelist",
                whitelistId.ToString(),
                additionalData: JsonSerializer.Serialize(new 
                { 
                    IpAddress = entry.IpAddress, 
                    Type = entry.Type.ToString(),
                    Notes = notes
                }));

            _logger.LogInformation("IP whitelist entry approved for {IpAddress} ({Type}) by user {UserId}", 
                entry.IpAddress, entry.Type, approvedByUserId);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving IP whitelist entry {WhitelistId}", whitelistId);
            return false;
        }
    }

    public async Task<bool> RevokeIpWhitelistAsync(Guid whitelistId)
    {
        try
        {
            var entry = await _ipWhitelistRepository.GetByIdAsync(whitelistId);
            if (entry == null)
                return false;

            entry.Revoke();
            _ipWhitelistRepository.Update(entry);
            await _unitOfWork.SaveChangesAsync();

            // Publish audit event
            await _auditEventPublisher.PublishSecurityEventAsync(
                null,
                "IpWhitelistEntryRevoked",
                "IpWhitelist",
                whitelistId.ToString(),
                additionalData: JsonSerializer.Serialize(new 
                { 
                    IpAddress = entry.IpAddress, 
                    Type = entry.Type.ToString() 
                }));

            _logger.LogInformation("IP whitelist entry revoked for {IpAddress} ({Type})", 
                entry.IpAddress, entry.Type);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error revoking IP whitelist entry {WhitelistId}", whitelistId);
            return false;
        }
    }

    public async Task<bool> IsIpWhitelistedAsync(string ipAddress, IpWhitelistType type)
    {
        try
        {
            var activeEntries = await _ipWhitelistRepository.GetActiveEntriesByTypeAsync(type);
            
            foreach (var entry in activeEntries)
            {
                if (entry.MatchesIpAddress(ipAddress))
                {
                    _logger.LogDebug("IP {IpAddress} matched whitelist entry {EntryId} for type {Type}", 
                        ipAddress, entry.Id, type);
                    return true;
                }
            }

            _logger.LogDebug("IP {IpAddress} not found in whitelist for type {Type}", ipAddress, type);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if IP {IpAddress} is whitelisted for type {Type}", ipAddress, type);
            return false; // Fail closed - deny access on error
        }
    }

    public async Task<List<IpWhitelist>> GetWhitelistEntriesAsync(IpWhitelistType? type = null, bool activeOnly = true)
    {
        try
        {
            if (type.HasValue)
            {
                return activeOnly 
                    ? await _ipWhitelistRepository.GetActiveEntriesByTypeAsync(type.Value)
                    : await _ipWhitelistRepository.GetEntriesByTypeAsync(type.Value);
            }

            return activeOnly 
                ? await _ipWhitelistRepository.GetActiveEntriesAsync()
                : await _ipWhitelistRepository.GetAllEntriesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving whitelist entries");
            return new List<IpWhitelist>();
        }
    }

    public async Task<List<IpWhitelist>> GetPendingApprovalsAsync()
    {
        try
        {
            return await _ipWhitelistRepository.GetPendingApprovalsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving pending IP whitelist approvals");
            return new List<IpWhitelist>();
        }
    }

    public async Task<bool> ExtendWhitelistEntryAsync(Guid whitelistId, DateTime newExpiryDate)
    {
        try
        {
            var entry = await _ipWhitelistRepository.GetByIdAsync(whitelistId);
            if (entry == null)
                return false;

            entry.Extend(newExpiryDate);
            _ipWhitelistRepository.Update(entry);
            await _unitOfWork.SaveChangesAsync();

            // Publish audit event
            await _auditEventPublisher.PublishSecurityEventAsync(
                null,
                "IpWhitelistEntryExtended",
                "IpWhitelist",
                whitelistId.ToString(),
                additionalData: JsonSerializer.Serialize(new 
                { 
                    IpAddress = entry.IpAddress, 
                    Type = entry.Type.ToString(),
                    NewExpiryDate = newExpiryDate
                }));

            _logger.LogInformation("IP whitelist entry extended for {IpAddress} until {ExpiryDate}", 
                entry.IpAddress, newExpiryDate);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extending IP whitelist entry {WhitelistId}", whitelistId);
            return false;
        }
    }

    public async Task CleanupExpiredEntriesAsync()
    {
        try
        {
            var expiredEntries = await _ipWhitelistRepository.GetExpiredEntriesAsync();
            var cleanedUpCount = 0;

            foreach (var entry in expiredEntries)
            {
                entry.Revoke();
                _ipWhitelistRepository.Update(entry);
                cleanedUpCount++;

                // Publish audit event - using security event since there's no system event method
                await _auditEventPublisher.PublishSecurityEventAsync(
                    null,
                    "IpWhitelistEntryExpired",
                    "IpWhitelist",
                    entry.Id.ToString(),
                    additionalData: JsonSerializer.Serialize(new { IpAddress = entry.IpAddress, Type = entry.Type.ToString() }));
            }
            
            if (cleanedUpCount > 0)
            {
                await _unitOfWork.SaveChangesAsync();
            }

            _logger.LogInformation("Cleaned up {Count} expired IP whitelist entries", cleanedUpCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired IP whitelist entries");
        }
    }

    public bool ValidateIpAddress(string ipAddress, string? ipRange = null)
    {
        try
        {
            // Validate main IP address
            if (!IPAddress.TryParse(ipAddress, out _))
                return false;

            // Validate IP range if provided (CIDR notation)
            if (!string.IsNullOrEmpty(ipRange))
            {
                var parts = ipRange.Split('/');
                if (parts.Length != 2)
                    return false;

                if (!IPAddress.TryParse(parts[0], out _))
                    return false;

                if (!int.TryParse(parts[1], out var prefixLength))
                    return false;

                // Validate prefix length based on IP version
                var networkIp = IPAddress.Parse(parts[0]);
                var maxPrefixLength = networkIp.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
                
                if (prefixLength < 0 || prefixLength > maxPrefixLength)
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<IpWhitelistStatistics> GetWhitelistStatisticsAsync()
    {
        try
        {
            var allEntries = await _ipWhitelistRepository.GetAllEntriesAsync();
            var activeEntries = allEntries.Where(e => e.IsValidForAccess()).ToList();
            var pendingEntries = allEntries.Where(e => !e.IsActive).ToList();
            var expiredEntries = allEntries.Where(e => e.IsExpired()).ToList();

            return new IpWhitelistStatistics
            {
                TotalActiveEntries = activeEntries.Count,
                PendingApprovals = pendingEntries.Count,
                ExpiredEntries = expiredEntries.Count,
                EntriesByType = activeEntries.GroupBy(e => e.Type)
                    .ToDictionary(g => g.Key, g => g.Count()),
                LastCleanupAt = DateTime.UtcNow // This would be tracked separately in production
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving IP whitelist statistics");
            return new IpWhitelistStatistics();
        }
    }
}