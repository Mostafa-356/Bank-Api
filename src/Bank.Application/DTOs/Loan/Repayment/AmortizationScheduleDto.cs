namespace Bank.Application.DTOs.Loan.Repayment;

/// <summary>
/// DTO for amortization schedule
/// </summary>
public class AmortizationSchedule
{
    public Guid LoanId { get; set; }
    public string LoanNumber { get; set; } = string.Empty;
    public decimal TotalInterest { get; set; }
    public decimal TotalPayments { get; set; }
    public List<AmortizationEntry> Schedule { get; set; } = new();
}

