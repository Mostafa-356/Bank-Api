namespace Bank.Application.DTOs.Loan.Approval;

/// <summary>
/// DTO for loan approval decision
/// </summary>
public class ApprovalDecision
{
    public bool IsApproved { get; set; }
    public decimal? ApprovedAmount { get; set; }
    public decimal? InterestRate { get; set; }
    public int? ApprovedTermInMonths { get; set; }
    public string? Conditions { get; set; }
    public string? RejectionReason { get; set; }
    public List<string> RequiredDocuments { get; set; } = new();
}

