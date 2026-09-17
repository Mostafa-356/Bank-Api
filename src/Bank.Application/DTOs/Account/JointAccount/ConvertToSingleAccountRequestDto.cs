namespace Bank.Application.DTOs.Account.JointAccount;

/// <summary>
/// Request to convert joint account to single account
/// </summary>
public class ConvertToSingleAccountRequest
{
    public Guid AccountId { get; set; }
    public Guid RemainingHolderId { get; set; }
    public string? Reason { get; set; }
}

