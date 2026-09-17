namespace Bank.Application.DTOs.Loan.Repayment;

/// <summary>
/// DTO for complete repayment schedule
/// </summary>
public class RepaymentSchedule
{
    public Guid LoanId { get; set; }
    public string LoanNumber { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal TotalInterest { get; set; }
    public decimal MonthlyPayment { get; set; }
    public int TotalPayments { get; set; }
    public List<RepaymentScheduleEntry> Schedule { get; set; } = new();
}

