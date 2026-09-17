namespace Bank.Application.DTOs.Account.JointAccount;

/// <summary>
/// Request to convert account to joint account
/// </summary>
public class ConvertToJointAccountRequest
{
    public Guid AccountId { get; set; }
    public bool RequiresMultipleSignatures { get; set; } = false;
    public decimal? MultipleSignatureThreshold { get; set; }
    public int MinimumSignaturesRequired { get; set; } = 2;
}

