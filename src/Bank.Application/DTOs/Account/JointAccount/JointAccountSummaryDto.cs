namespace Bank.Application.DTOs.Account.JointAccount;

/// <summary>
/// Joint account summary information
/// </summary>
public class JointAccountSummary
{
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public bool IsJointAccount { get; set; }
    public bool RequiresMultipleSignatures { get; set; }
    public decimal? MultipleSignatureThreshold { get; set; }
    public int MinimumSignaturesRequired { get; set; }
    public int ActiveJointHoldersCount { get; set; }
    public List<JointAccountHolderDto> JointHolders { get; set; } = new();
}

