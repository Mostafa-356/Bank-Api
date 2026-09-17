namespace Bank.Application.DTOs.Shared.RateLimit;

/// <summary>
/// Current status of rate limiting for a key
/// </summary>
public class RateLimitStatus
{
    /// <summary>
    /// Current number of requests in the window
    /// </summary>
    public int RequestCount { get; set; }

    /// <summary>
    /// Maximum requests allowed in the window
    /// </summary>
    public int RequestLimit { get; set; }

    /// <summary>
    /// Duration of the rate limiting window
    /// </summary>
    public TimeSpan WindowDuration { get; set; }

    /// <summary>
    /// Start time of the current window
    /// </summary>
    public DateTime WindowStart { get; set; }

    /// <summary>
    /// End time of the current window
    /// </summary>
    public DateTime WindowEnd { get; set; }

    /// <summary>
    /// Whether the rate limit is currently exceeded
    /// </summary>
    public bool IsExceeded => RequestCount >= RequestLimit;

    /// <summary>
    /// Number of requests remaining in the current window
    /// </summary>
    public int RequestsRemaining => Math.Max(0, RequestLimit - RequestCount);
}

