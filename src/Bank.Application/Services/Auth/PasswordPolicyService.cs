using System.Security.Cryptography;
using System.Text;
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
/// Service for managing password policies and validation
/// </summary>
public sealed class PasswordPolicyService : IPasswordPolicyService
{
    private readonly IPasswordPolicyRepository _passwordPolicyRepository;
    private readonly IPasswordHistoryRepository _passwordHistoryRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAuditEventPublisher _auditEventPublisher;
    private readonly ILogger<PasswordPolicyService> _logger;
    private readonly IUnitOfWork _unitOfWork;

    // Common passwords list (in production, this would be loaded from a file or database)
    private readonly HashSet<string> _commonPasswords = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "123456", "password123", "admin", "qwerty", "letmein", "welcome",
        "monkey", "1234567890", "abc123", "111111", "123123", "password1", "1234",
        "12345", "dragon", "master", "hello", "login", "welcome123", "admin123"
    };

    public PasswordPolicyService(
        IPasswordPolicyRepository passwordPolicyRepository,
        IPasswordHistoryRepository passwordHistoryRepository,
        IUserRepository userRepository,
        IAuditEventPublisher auditEventPublisher,
        ILogger<PasswordPolicyService> logger,
        IUnitOfWork unitOfWork)
    {
        _passwordPolicyRepository = passwordPolicyRepository;
        _passwordHistoryRepository = passwordHistoryRepository;
        _userRepository = userRepository;
        _auditEventPublisher = auditEventPublisher;
        _logger = logger;
        _unitOfWork = unitOfWork;
    }

    public async Task<PasswordValidationResult> ValidatePasswordAsync(string password, Guid userId, PasswordComplexityLevel? complexityLevel = null)
    {
        try
        {
            var policy = complexityLevel.HasValue 
                ? await GetPasswordPolicyAsync(complexityLevel.Value)
                : await GetDefaultPasswordPolicyAsync();

            if (policy == null)
            {
                return new PasswordValidationResult
                {
                    IsValid = false,
                    Errors = { "No password policy found" }
                };
            }

            return await ValidatePasswordAsync(password, userId, policy.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating password for user {UserId}", userId);
            return new PasswordValidationResult
            {
                IsValid = false,
                Errors = { "Password validation failed" }
            };
        }
    }

    public async Task<PasswordValidationResult> ValidatePasswordAsync(string password, Guid userId, Guid policyId)
    {
        try
        {
            var policy = await _passwordPolicyRepository.GetByIdAsync(policyId);
            if (policy == null)
            {
                return new PasswordValidationResult
                {
                    IsValid = false,
                    Errors = { "Password policy not found" }
                };
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return new PasswordValidationResult
                {
                    IsValid = false,
                    Errors = { "User not found" }
                };
            }

            var result = new PasswordValidationResult
            {
                RequiredComplexityLevel = policy.ComplexityLevel
            };

            var errors = new List<string>();

            // Perform all validation checks
            ValidateLengthRequirements(password, policy, errors);
            ValidateCharacterRequirements(password, policy, errors);
            ValidateUniqueCharacters(password, policy, errors);
            await ValidatePasswordHistoryAndPersonalInfo(password, userId, user, policy, result, errors);

            // Calculate password strength score
            result.PasswordStrengthScore = CalculatePasswordStrength(password);
            result.Errors = errors;
            result.IsValid = errors.Count == 0;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating password against policy {PolicyId} for user {UserId}", policyId, userId);
            return new PasswordValidationResult
            {
                IsValid = false,
                Errors = { "Password validation failed" }
            };
        }
    }

    private static void ValidateLengthRequirements(string password, PasswordPolicy policy, List<string> errors)
    {
        if (password.Length < policy.MinimumLength)
        {
            errors.Add($"Password must be at least {policy.MinimumLength} characters long");
        }

        if (password.Length > policy.MaximumLength)
        {
            errors.Add($"Password must not exceed {policy.MaximumLength} characters");
        }
    }

    private static void ValidateCharacterRequirements(string password, PasswordPolicy policy, List<string> errors)
    {
        if (policy.RequireUppercase && !password.Any(char.IsUpper))
        {
            errors.Add("Password must contain at least one uppercase letter");
        }

        if (policy.RequireLowercase && !password.Any(char.IsLower))
        {
            errors.Add("Password must contain at least one lowercase letter");
        }

        if (policy.RequireDigits && !password.Any(char.IsDigit))
        {
            errors.Add("Password must contain at least one digit");
        }

        if (policy.RequireSpecialCharacters && !password.Any(c => policy.AllowedSpecialCharacters.Contains(c)))
        {
            errors.Add($"Password must contain at least one special character from: {policy.AllowedSpecialCharacters}");
        }
    }

    private static void ValidateUniqueCharacters(string password, PasswordPolicy policy, List<string> errors)
    {
        var uniqueChars = password.Distinct().Count();
        if (uniqueChars < policy.MinimumUniqueCharacters)
        {
            errors.Add($"Password must contain at least {policy.MinimumUniqueCharacters} unique characters");
        }
    }

    private async Task ValidatePasswordHistoryAndPersonalInfo(string password, Guid userId, User user, PasswordPolicy policy, PasswordValidationResult result, List<string> errors)
    {
        // Common password check
        if (policy.PreventCommonPasswords && IsCommonPassword(password))
        {
            errors.Add("Password is too common and easily guessable");
            result.IsCommonPassword = true;
        }

        // User info check
        if (policy.PreventUserInfoInPassword && ContainsUserInfo(password, user))
        {
            errors.Add("Password must not contain personal information");
            result.ContainsUserInfo = true;
        }

        // Password history check
        var passwordHash = HashPassword(password);
        var isRecentlyUsed = await IsPasswordRecentlyUsedAsync(userId, passwordHash);
        if (isRecentlyUsed)
        {
            errors.Add($"Password has been used recently. Please choose a different password");
            result.IsPasswordRecentlyUsed = true;
        }
    }

    public async Task<PasswordPolicy?> GetDefaultPasswordPolicyAsync()
    {
        try
        {
            return await _passwordPolicyRepository.GetDefaultPolicyAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving default password policy");
            return null;
        }
    }

    public async Task<PasswordPolicy?> GetPasswordPolicyAsync(PasswordComplexityLevel complexityLevel)
    {
        try
        {
            return await _passwordPolicyRepository.GetByComplexityLevelAsync(complexityLevel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving password policy for complexity level {ComplexityLevel}", complexityLevel);
            return null;
        }
    }

    public async Task<List<PasswordPolicy>> GetActivePasswordPoliciesAsync()
    {
        try
        {
            return await _passwordPolicyRepository.GetActivePoliciesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active password policies");
            return new List<PasswordPolicy>();
        }
    }

    public async Task<PasswordPolicy> CreatePasswordPolicyAsync(PasswordPolicy policy)
    {
        try
        {
            await _passwordPolicyRepository.AddAsync(policy);
            await _unitOfWork.SaveChangesAsync();

            await _auditEventPublisher.PublishSecurityEventAsync(
                null,
                "PasswordPolicyCreated",
                "PasswordPolicy",
                policy.Id.ToString(),
                additionalData: JsonSerializer.Serialize(new { PolicyName = policy.Name, ComplexityLevel = policy.ComplexityLevel }));

            _logger.LogInformation("Password policy {PolicyName} created with complexity level {ComplexityLevel}", 
                policy.Name, policy.ComplexityLevel);

            return policy;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating password policy {PolicyName}", policy.Name);
            throw;
        }
    }

    public async Task<bool> UpdatePasswordPolicyAsync(Guid policyId, PasswordPolicy updatedPolicy)
    {
        try
        {
            var existingPolicy = await _passwordPolicyRepository.GetByIdAsync(policyId);
            if (existingPolicy == null)
                return false;

            // Update the policy properties
            existingPolicy.UpdatePolicy(
                updatedPolicy.MinimumLength,
                updatedPolicy.MaximumLength,
                updatedPolicy.RequireUppercase,
                updatedPolicy.RequireLowercase,
                updatedPolicy.RequireDigits,
                updatedPolicy.RequireSpecialCharacters,
                updatedPolicy.MinimumUniqueCharacters,
                updatedPolicy.PreventCommonPasswords,
                updatedPolicy.PreventUserInfoInPassword,
                updatedPolicy.PasswordHistoryCount,
                updatedPolicy.MaxPasswordAge,
                updatedPolicy.MinPasswordAge,
                updatedPolicy.MaxFailedAttempts,
                updatedPolicy.LockoutDuration);

            _passwordPolicyRepository.Update(existingPolicy);
            await _unitOfWork.SaveChangesAsync();

            await _auditEventPublisher.PublishSecurityEventAsync(
                null,
                "PasswordPolicyUpdated",
                "PasswordPolicy",
                policyId.ToString(),
                additionalData: JsonSerializer.Serialize(new { PolicyName = existingPolicy.Name }));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating password policy {PolicyId}", policyId);
            return false;
        }
    }

    public async Task<bool> SetDefaultPasswordPolicyAsync(Guid policyId)
    {
        try
        {
            var policy = await _passwordPolicyRepository.GetByIdAsync(policyId);
            if (policy == null)
                return false;

            // Clear existing default
            await _passwordPolicyRepository.ClearDefaultPolicyAsync();

            // Set new default
            policy.SetAsDefault();
            _passwordPolicyRepository.Update(policy);
            await _unitOfWork.SaveChangesAsync();

            await _auditEventPublisher.PublishSecurityEventAsync(
                null,
                "DefaultPasswordPolicyChanged",
                "PasswordPolicy",
                policyId.ToString(),
                additionalData: JsonSerializer.Serialize(new { PolicyName = policy.Name }));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting default password policy {PolicyId}", policyId);
            return false;
        }
    }

    public async Task<bool> IsPasswordChangeRequiredAsync(Guid userId)
    {
        try
        {
            var currentPasswordHistory = await _passwordHistoryRepository.GetCurrentPasswordAsync(userId);
            if (currentPasswordHistory == null)
                return true; // No password set

            var policy = await GetDefaultPasswordPolicyAsync();
            if (policy == null)
                return false;

            var passwordAge = DateTime.UtcNow - currentPasswordHistory.PasswordSetAt;
            return passwordAge > policy.MaxPasswordAge;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if password change is required for user {UserId}", userId);
            return false;
        }
    }

    public async Task RecordPasswordChangeAsync(Guid userId, string passwordHash, string? passwordSalt = null)
    {
        try
        {
            // Mark current password as old
            await _passwordHistoryRepository.MarkCurrentPasswordAsOldAsync(userId);

            // Add new password to history
            var passwordHistory = new PasswordHistory(userId, passwordHash, passwordSalt, true);
            await _passwordHistoryRepository.AddAsync(passwordHistory);
            await _unitOfWork.SaveChangesAsync();

            // Clean up old password history based on policy
            var policy = await GetDefaultPasswordPolicyAsync();
            if (policy != null)
            {
                await CleanupPasswordHistoryAsync(userId, policy.PasswordHistoryCount);
            }

            await _auditEventPublisher.PublishSecurityEventAsync(
                userId,
                "PasswordChanged",
                "User",
                userId.ToString());

            _logger.LogInformation("Password changed for user {UserId}", userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording password change for user {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> IsPasswordRecentlyUsedAsync(Guid userId, string passwordHash)
    {
        try
        {
            var policy = await GetDefaultPasswordPolicyAsync();
            if (policy == null)
                return false;

            var recentPasswords = await _passwordHistoryRepository.GetRecentPasswordsAsync(userId, policy.PasswordHistoryCount);
            return recentPasswords.Any(p => p.PasswordHash == passwordHash);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if password was recently used for user {UserId}", userId);
            return false;
        }
    }

    public async Task<List<PasswordHistory>> GetPasswordHistoryAsync(Guid userId, int? limit = null)
    {
        try
        {
            return await _passwordHistoryRepository.GetPasswordHistoryAsync(userId, limit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving password history for user {UserId}", userId);
            return new List<PasswordHistory>();
        }
    }

    public async Task CleanupPasswordHistoryAsync(Guid userId, int keepCount)
    {
        try
        {
            await _passwordHistoryRepository.CleanupOldPasswordsAsync(userId, keepCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up password history for user {UserId}", userId);
        }
    }

    public async Task<string> GenerateSecurePasswordAsync(PasswordComplexityLevel complexityLevel)
    {
        try
        {
            var policy = await GetPasswordPolicyAsync(complexityLevel) ?? await GetDefaultPasswordPolicyAsync();
            if (policy == null)
                throw new InvalidOperationException("No password policy available");

            return GeneratePassword(policy);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating secure password for complexity level {ComplexityLevel}", complexityLevel);
            throw;
        }
    }

    public bool IsCommonPassword(string password)
    {
        return _commonPasswords.Contains(password);
    }

    public bool ContainsUserInfo(string password, User user)
    {
        var passwordLower = password.ToLowerInvariant();
        
        // Check if password contains user's name, email parts, or username
        var userInfo = new[]
        {
            user.FirstName?.ToLowerInvariant(),
            user.LastName?.ToLowerInvariant(),
            user.UserName?.ToLowerInvariant(),
            user.Email?.Split('@')[0]?.ToLowerInvariant()
        }.Where(info => !string.IsNullOrEmpty(info) && info.Length >= 3);

        return userInfo.Any(info => !string.IsNullOrEmpty(info) && passwordLower.Contains(info));
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    private static int CalculatePasswordStrength(string password)
    {
        var score = 0;

        // Length scoring
        if (password.Length >= 8) score += 10;
        if (password.Length >= 12) score += 10;
        if (password.Length >= 16) score += 10;

        // Character variety scoring
        if (password.Any(char.IsUpper)) score += 15;
        if (password.Any(char.IsLower)) score += 15;
        if (password.Any(char.IsDigit)) score += 15;
        if (password.Any(c => "!@#$%^&*()_+-=[]{}|;:,.<>?".Contains(c))) score += 15;

        // Unique characters
        var uniqueRatio = (double)password.Distinct().Count() / password.Length;
        score += (int)(uniqueRatio * 10);

        return Math.Min(score, 100);
    }

    private static string GeneratePassword(PasswordPolicy policy)
    {
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        var password = new StringBuilder();

        var lowercase = "abcdefghijklmnopqrstuvwxyz";
        var uppercase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var digits = "0123456789";
        var special = policy.AllowedSpecialCharacters;

        var allChars = "";
        
        // Ensure required character types are included
        if (policy.RequireLowercase)
        {
            password.Append(lowercase[GetSecureRandomInt(rng, lowercase.Length)]);
            allChars += lowercase;
        }
        
        if (policy.RequireUppercase)
        {
            password.Append(uppercase[GetSecureRandomInt(rng, uppercase.Length)]);
            allChars += uppercase;
        }
        
        if (policy.RequireDigits)
        {
            password.Append(digits[GetSecureRandomInt(rng, digits.Length)]);
            allChars += digits;
        }
        
        if (policy.RequireSpecialCharacters)
        {
            password.Append(special[GetSecureRandomInt(rng, special.Length)]);
            allChars += special;
        }

        // Fill remaining length
        while (password.Length < policy.MinimumLength)
        {
            password.Append(allChars[GetSecureRandomInt(rng, allChars.Length)]);
        }

        // Shuffle the password
        var passwordArray = password.ToString().ToCharArray();
        for (int i = passwordArray.Length - 1; i > 0; i--)
        {
            int j = GetSecureRandomInt(rng, i + 1);
            (passwordArray[i], passwordArray[j]) = (passwordArray[j], passwordArray[i]);
        }

        return new string(passwordArray);
    }

    private static int GetSecureRandomInt(System.Security.Cryptography.RandomNumberGenerator rng, int maxValue)
    {
        var bytes = new byte[4];
        rng.GetBytes(bytes);
        var value = Math.Abs(BitConverter.ToInt32(bytes, 0));
        return value % maxValue;
    }
}