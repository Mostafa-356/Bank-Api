namespace Bank.Application.DTOs.Shared.RateLimit;

/// <summary>
/// Rate limiting policy configuration
/// </summary>
public class RateLimitPolicy
{
    /// <summary>
    /// Maximum number of requests allowed in the window
    /// </summary>
    public int RequestLimit { get; set; }

    /// <summary>
    /// Duration of the rate limiting window
    /// </summary>
    public TimeSpan WindowDuration { get; set; }

    /// <summary>
    /// Default policy for general API requests (100 requests per minute)
    /// </summary>
    public static RateLimitPolicy Default => new()
    {
        RequestLimit = 100,
        WindowDuration = TimeSpan.FromMinutes(1)
    };

    /// <summary>
    /// Policy for login attempts (5 attempts per 15 minutes)
    /// </summary>
    public static RateLimitPolicy Login => new()
    {
        RequestLimit = 5,
        WindowDuration = TimeSpan.FromMinutes(15)
    };

    /// <summary>
    /// Policy for transaction requests (50 requests per minute)
    /// </summary>
    public static RateLimitPolicy Transaction => new()
    {
        RequestLimit = 50,
        WindowDuration = TimeSpan.FromMinutes(1)
    };

    /// <summary>
    /// Policy for two-factor authentication attempts (10 attempts per 5 minutes)
    /// </summary>
    public static RateLimitPolicy TwoFactor => new()
    {
        RequestLimit = 10,
        WindowDuration = TimeSpan.FromMinutes(5)
    };

    /// <summary>
    /// Strict policy for sensitive operations (10 requests per hour)
    /// </summary>
    public static RateLimitPolicy Strict => new()
    {
        RequestLimit = 10,
        WindowDuration = TimeSpan.FromHours(1)
    };
}

