namespace Bank.Application.DTOs.Account.Transfer;

/// <summary>
/// Transfer eligibility check request
/// </summary>
public class TransferEligibilityRequest
{
    public Guid BeneficiaryId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime ProposedTransferDate { get; set; } = DateTime.UtcNow;
}

