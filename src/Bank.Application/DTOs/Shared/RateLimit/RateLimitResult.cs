namespace Bank.Application.DTOs.Shared.RateLimit;

/// <summary>
/// Result of a rate limit check
/// </summary>
public class RateLimitResult
{
    /// <summary>
    /// Whether the request is allowed
    /// </summary>
    public bool IsAllowed { get; set; }

    /// <summary>
    /// Number of requests remaining in the current window
    /// </summary>
    public int RequestsRemaining { get; set; }

    /// <summary>
    /// Time until the rate limit window resets
    /// </summary>
    public TimeSpan ResetTime { get; set; }

    /// <summary>
    /// Optional message explaining the rate limit status
    /// </summary>
    public string? Message { get; set; }
}

