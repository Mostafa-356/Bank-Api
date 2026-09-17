namespace Bank.Application.DTOs.Loan.Configuration;

/// <summary>
/// DTO for early payoff calculation
/// </summary>
public class EarlyPayoffCalculation
{
    public Guid LoanId { get; set; }
    public DateTime PayoffDate { get; set; }
    public decimal CurrentBalance { get; set; }
    public decimal AccruedInterest { get; set; }
    public decimal PrepaymentPenalty { get; set; }
    public decimal TotalPayoffAmount { get; set; }
    public decimal InterestSavings { get; set; }
    public int PaymentsSaved { get; set; }
}

