namespace Bank.Application.DTOs.Account.Transfer;

/// <summary>
/// Limit check result
/// </summary>
public class LimitCheckResult
{
    public bool WithinLimit { get; set; }
    public decimal? Limit { get; set; }
    public decimal CurrentUsage { get; set; }
    public decimal RequestedAmount { get; set; }
    public decimal? RemainingLimit { get; set; }
    public string LimitType { get; set; } = string.Empty; // "Daily", "Monthly", "Single"
    public List<string> LimitIssues { get; set; } = new();
}

