using Bank.Application.DTOs.Common;
using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Loan.Approval;

/// <summary>
/// DTO for loan approval result
/// </summary>
public class LoanApprovalResult : BaseResultDto
{
    public LoanStatus NewStatus { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public decimal? InterestRate { get; set; }
    public decimal? MonthlyPayment { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public List<string> NextSteps { get; set; } = new();
}


