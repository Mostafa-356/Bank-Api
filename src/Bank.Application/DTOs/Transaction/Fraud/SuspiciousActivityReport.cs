namespace Bank.Application.DTOs.Transaction.Fraud;

/// <summary>
/// Report for suspicious activity
/// </summary>
public class SuspiciousActivityReport
{
    /// <summary>
    /// User ID associated with the suspicious activity
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Type of suspicious activity
    /// </summary>
    public string ActivityType { get; set; } = string.Empty;

    /// <summary>
    /// Description of the suspicious activity
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Risk score associated with the activity
    /// </summary>
    public decimal RiskScore { get; set; }

    /// <summary>
    /// IP address where the activity occurred
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent of the client
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Additional metadata about the activity
    /// </summary>
    public Dictionary<string, object> Metadata { get; set; } = new();
}

