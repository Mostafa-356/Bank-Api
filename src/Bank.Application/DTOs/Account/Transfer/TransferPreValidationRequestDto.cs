namespace Bank.Application.DTOs.Account.Transfer;

/// <summary>
/// Transfer pre-validation request
/// </summary>
public class TransferPreValidationRequest
{
    public Guid FromAccountId { get; set; }
    public Guid BeneficiaryId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime ProposedTransferDate { get; set; } = DateTime.UtcNow;
    public string? Purpose { get; set; }
    public bool CheckAccountBalance { get; set; } = true;
    public bool CheckBeneficiaryStatus { get; set; } = true;
    public bool CheckTransferLimits { get; set; } = true;
    public bool CheckFraudRules { get; set; } = true;
}

