namespace Bank.Application.DTOs.Loan.Repayment;

/// <summary>
/// DTO for amortization schedule entry
/// </summary>
public class AmortizationEntry
{
    public int PaymentNumber { get; set; }
    public DateTime PaymentDate { get; set; }
    public decimal PaymentAmount { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal InterestAmount { get; set; }
    public decimal RemainingBalance { get; set; }
    public decimal CumulativeInterest { get; set; }
    public decimal CumulativePrincipal { get; set; }
}

