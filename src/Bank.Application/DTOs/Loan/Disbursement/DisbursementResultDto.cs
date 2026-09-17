using Bank.Application.DTOs.Common;

namespace Bank.Application.DTOs.Loan.Disbursement;

/// <summary>
/// DTO for loan disbursement result
/// </summary>
public class DisbursementResult : BaseResultDto
{
    public decimal DisbursedAmount { get; set; }
    public string? TransactionReference { get; set; }
    public DateTime DisbursementDate { get; set; }
    public DateTime FirstPaymentDueDate { get; set; }
    public decimal MonthlyPaymentAmount { get; set; }
}


