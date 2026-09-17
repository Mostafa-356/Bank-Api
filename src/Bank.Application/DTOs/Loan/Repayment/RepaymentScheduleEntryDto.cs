namespace Bank.Application.DTOs.Loan.Repayment;

/// <summary>
/// DTO for repayment schedule entry
/// </summary>
public class RepaymentScheduleEntry
{
    public int PaymentNumber { get; set; }
    public DateTime DueDate { get; set; }
    public decimal PaymentAmount { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal InterestAmount { get; set; }
    public decimal RemainingBalance { get; set; }
    public bool IsPaid { get; set; }
    public DateTime? PaidDate { get; set; }
}

