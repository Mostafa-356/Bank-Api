using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Transaction.Fraud;

/// <summary>
/// Fraud detection rule configuration
/// </summary>
public class FraudRule
{
    /// <summary>
    /// Unique identifier for the rule
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable name of the rule
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Description of what the rule detects
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Whether the rule is currently active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Type of fraud rule
    /// </summary>
    public FraudRuleType Type { get; set; }

    /// <summary>
    /// Rule-specific parameters
    /// </summary>
    public Dictionary<string, object> Parameters { get; set; } = new();

    /// <summary>
    /// Risk score to add when this rule is triggered
    /// </summary>
    public decimal RiskScore { get; set; }

    /// <summary>
    /// Action to take when this rule is triggered
    /// </summary>
    public string? Action { get; set; }
}

