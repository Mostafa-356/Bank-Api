using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Transaction.Fraud;

/// <summary>
/// Result of fraud analysis for a transaction or activity
/// </summary>
public class FraudAnalysisResult
{
    /// <summary>
    /// Whether the activity is considered suspicious
    /// </summary>
    public bool IsSuspicious { get; set; }

    /// <summary>
    /// Risk level assessment
    /// </summary>
    public FraudRiskLevel RiskLevel { get; set; }

    /// <summary>
    /// Numerical risk score (0-100)
    /// </summary>
    public decimal RiskScore { get; set; }

    /// <summary>
    /// List of fraud detection rules that were triggered
    /// </summary>
    public List<string> TriggeredRules { get; set; } = new();

    /// <summary>
    /// Recommended action to take
    /// </summary>
    public string? RecommendedAction { get; set; }

    /// <summary>
    /// Whether the transaction should be blocked
    /// </summary>
    public bool ShouldBlock { get; set; }

    /// <summary>
    /// Whether the account should be frozen
    /// </summary>
    public bool ShouldFreeze { get; set; }

    /// <summary>
    /// Additional message or explanation
    /// </summary>
    public string? Message { get; set; }
}

