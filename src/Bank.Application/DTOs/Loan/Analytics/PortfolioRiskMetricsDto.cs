using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Loan.Analytics;

/// <summary>
/// DTO for portfolio risk metrics
/// </summary>
public class PortfolioRiskMetrics
{
    public decimal TotalExposure { get; set; }
    public decimal WeightedAverageRisk { get; set; }
    public decimal ConcentrationRisk { get; set; }
    public Dictionary<LoanType, decimal> RiskByType { get; set; } = new();
    public Dictionary<CreditScoreRange, decimal> RiskByScore { get; set; } = new();
    public decimal VaR95 { get; set; } // Value at Risk 95%
    public decimal ExpectedLoss { get; set; }
    public DateTime CalculationDate { get; set; }
}

