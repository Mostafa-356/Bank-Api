using Bank.Application.Helpers.Shared;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Bank.Application.Services;

/// <summary>
/// Service for secure logging of sensitive information without exposing actual values.
/// Delegates to centralized MaskingHelper for consistent masking patterns.
/// </summary>
public static class SecureLoggingService
{
    /// <summary>
    /// Hash a sensitive value for logging purposes (one-way)
    /// </summary>
    public static string HashForLogging(Guid sensitiveValue)
    {
        using (var sha256 = SHA256.Create())
        {
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sensitiveValue.ToString()));
            return Convert.ToHexString(hash)[..8]; // First 8 characters
        }
    }

    /// <summary>
    /// Hash a sensitive value for logging purposes (one-way)
    /// </summary>
    public static string HashForLogging(string sensitiveValue)
    {
        if (string.IsNullOrEmpty(sensitiveValue))
            return "[empty]";

        using (var sha256 = SHA256.Create())
        {
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(sensitiveValue));
            return Convert.ToHexString(hash)[..8]; // First 8 characters
        }
    }

    /// <summary>
    /// Mask a GUID for logging (show only first segment)
    /// </summary>
    public static string MaskGuid(Guid value)
    {
        var guidStr = value.ToString();
        var firstSegment = guidStr.Split('-')[0];
        return $"[{firstSegment}...]";
    }

    /// <summary>
    /// Mask an email address (delegates to centralized MaskingHelper)
    /// </summary>
    public static string MaskEmail(string email)
    {
        return MaskingHelper.MaskEmail(email);
    }

    /// <summary>
    /// Mask a card number (delegates to centralized MaskingHelper)
    /// </summary>
    public static string MaskCardNumber(string cardNumber)
    {
        return MaskingHelper.MaskCardNumber(cardNumber);
    }

    /// <summary>
    /// Create a generic error message without sensitive details
    /// </summary>
    public static void LogErrorSecurely(ILogger logger, Exception ex, string operation)
    {
        logger.LogError(ex, "Error during {Operation}", operation);
    }

    /// <summary>
    /// Create a generic warning message without sensitive details
    /// </summary>
    public static void LogWarningSecurely(ILogger logger, string operation)
    {
        logger.LogWarning("Warning during {Operation}", operation);
    }

    /// <summary>
    /// Sanitizes user input for logging to prevent log injection (CWE-117).
    /// </summary>
    public static string SanitizeInput(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        return input.Replace("\r", "").Replace("\n", " ");
    }
}
