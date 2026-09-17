namespace Bank.Application.DTOs.Loan.Repayment;

/// <summary>
/// DTO for loan payment request
/// </summary>
public class LoanPaymentRequest
{
    public Guid LoanId { get; set; }
    public decimal PaymentAmount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

