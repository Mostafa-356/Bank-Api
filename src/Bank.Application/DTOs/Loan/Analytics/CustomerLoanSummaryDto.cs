namespace Bank.Application.DTOs.Loan.Analytics;

/// <summary>
/// DTO for customer loan summary
/// </summary>
public class CustomerLoanSummary
{
    public Guid CustomerId { get; set; }
    public int TotalLoans { get; set; }
    public int ActiveLoans { get; set; }
    public decimal TotalBorrowed { get; set; }
    public decimal TotalOutstanding { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal WeightedAverageRate { get; set; }
    public int OnTimePayments { get; set; }
    public int LatePayments { get; set; }
    public decimal PaymentReliabilityScore { get; set; }
    public LoanRiskLevel RiskLevel { get; set; }
    public List<LoanDto> Loans { get; set; } = new();
}

