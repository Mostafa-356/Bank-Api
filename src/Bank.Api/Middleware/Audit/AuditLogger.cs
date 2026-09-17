using Bank.Application.Interfaces;
using System.Security.Claims;
using System.Text.Json;

namespace Bank.Api.Middleware.Audit;

/// <summary>
/// Represents captured audit entry data for logging
/// </summary>
public class AuditEntryData
{
    public Guid? UserId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? SessionId { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string RequestDetails { get; set; } = string.Empty;
    public string ResponseDetails { get; set; } = string.Empty;
    public long DurationMs { get; set; }
    public int StatusCode { get; set; }
    public string RequestId { get; set; } = string.Empty;
}

/// <summary>
/// Represents captured security event data for logging
/// </summary>
public class SecurityEventData
{
    public Guid? UserId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? SessionId { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string AdditionalData { get; set; } = string.Empty;
    public string RequestId { get; set; } = string.Empty;
}

/// <summary>
/// Handles logging of audit entries and security events
/// </summary>
public class AuditLogger
{
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(ILogger<AuditLogger> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Logs an audit entry using pre-captured data (safe to call after HttpContext disposal).
    /// </summary>
    public void LogAuditEntryDirect(AuditEntryData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        var action = $"{data.Method} {data.Path}";

        _logger.LogInformation(
            "Audit: {Action} | User: {UserId} | IP: {IpAddress} | Status: {StatusCode} | Duration: {DurationMs}ms | RequestId: {RequestId}",
            Bank.Application.Services.SecureLoggingService.SanitizeInput(action), Bank.Application.Services.SecureLoggingService.SanitizeInput(data.UserId?.ToString() ?? "anonymous"), Bank.Application.Services.SecureLoggingService.SanitizeInput(data.IpAddress ?? "unknown"), data.StatusCode, data.DurationMs, data.RequestId);
    }

    /// <summary>
    /// Logs a security event using pre-captured data (safe to call after HttpContext disposal).
    /// </summary>
    public void LogSecurityEventDirect(SecurityEventData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));

        _logger.LogWarning(
            "SecurityEvent: {Action} | User: {UserId} | IP: {IpAddress} | Path: {Path} | RequestId: {RequestId} | Details: {Details}",
            Bank.Application.Services.SecureLoggingService.SanitizeInput(data.Action ?? "unknown"), Bank.Application.Services.SecureLoggingService.SanitizeInput(data.UserId?.ToString() ?? "anonymous"), Bank.Application.Services.SecureLoggingService.SanitizeInput(data.IpAddress ?? "unknown"), Bank.Application.Services.SecureLoggingService.SanitizeInput(data.Path ?? "unknown"), data.RequestId, Bank.Application.Services.SecureLoggingService.SanitizeInput(data.AdditionalData ?? "none"));
    }

    private static Guid? GetUserId(HttpContext context)
    {
        var userIdClaim = context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private static string? GetClientIpAddress(HttpContext context)
    {
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
        if (!string.IsNullOrEmpty(realIp))
        {
            return realIp;
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }
}
