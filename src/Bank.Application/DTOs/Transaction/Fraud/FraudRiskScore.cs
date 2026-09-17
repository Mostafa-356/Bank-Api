using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Transaction.Fraud;

/// <summary>
/// Fraud risk score for a user
/// </summary>
public class FraudRiskScore
{
    /// <summary>
    /// User ID
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Numerical risk score (0-100)
    /// </summary>
    public decimal Score { get; set; }

    /// <summary>
    /// Risk level based on the score
    /// </summary>
    public FraudRiskLevel Level { get; set; }

    /// <summary>
    /// When the score was calculated
    /// </summary>
    public DateTime CalculatedAt { get; set; }

    /// <summary>
    /// Factors that contributed to the risk score
    /// </summary>
    public List<string> RiskFactors { get; set; } = new();
}

