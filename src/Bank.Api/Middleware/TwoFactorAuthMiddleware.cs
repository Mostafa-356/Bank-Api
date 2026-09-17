using System.Security.Claims;
using Bank.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;

namespace Bank.Api.Middleware;

/// <summary>
/// Middleware to enforce two-factor authentication for protected endpoints
/// </summary>
public class TwoFactorAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TwoFactorAuthMiddleware> _logger;

    public TwoFactorAuthMiddleware(RequestDelegate next, ILogger<TwoFactorAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITwoFactorAuthService twoFactorService)
    {
        // Skip 2FA check for certain endpoints
        if (ShouldSkipTwoFactorCheck(context))
        {
            await _next(context);
            return;
        }

        // Check if user is authenticated
        if (!context.User.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        // Get user ID from claims
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
        {
            await _next(context);
            return;
        }

        // Check if user has 2FA enabled
        var is2FAEnabled = await twoFactorService.IsTwoFactorEnabledAsync(userId);
        if (!is2FAEnabled)
        {
            await _next(context);
            return;
        }

        // Check if 2FA is already verified for this session
        var is2FAVerified = context.User.HasClaim("2fa_verified", "true");
        if (is2FAVerified)
        {
            await _next(context);
            return;
        }

        // 2FA verification required
        _logger.LogWarning("2FA verification required for user {UserId} accessing {Path}", userId, Bank.Application.Services.SecureLoggingService.SanitizeInput(context.Request.Path));
        
        context.Response.StatusCode = 403;
        await context.Response.WriteAsync("Two-factor authentication verification required");
    }

    private static bool ShouldSkipTwoFactorCheck(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant();
        
        // Skip 2FA check for these endpoints
        var skipPaths = new[]
        {
            "/api/auth/login",
            "/api/auth/register",
            "/api/auth/2fa",
            "/api/auth/verify-2fa",
            "/health",
            "/swagger",
            "/api/auth/refresh"
        };

        return skipPaths.Any(skipPath => path?.StartsWith(skipPath) == true);
    }
}