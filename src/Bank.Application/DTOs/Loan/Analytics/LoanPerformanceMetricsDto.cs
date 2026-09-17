namespace Bank.Application.DTOs.Loan.Analytics;

/// <summary>
/// DTO for loan performance metrics
/// </summary>
public class LoanPerformanceMetrics
{
    public Guid LoanId { get; set; }
    public string LoanNumber { get; set; } = string.Empty;
    public decimal PaymentToIncomeRatio { get; set; }
    public decimal DebtToIncomeRatio { get; set; }
    public int PaymentHistory { get; set; } // Number of on-time payments
    public int MissedPayments { get; set; }
    public decimal TotalInterestPaid { get; set; }
    public decimal PercentagePaid { get; set; }
    public DateTime LastPaymentDate { get; set; }
    public LoanRiskLevel RiskLevel { get; set; }
}

