namespace Bank.Application.DTOs.Account.JointAccount;

/// <summary>
/// Request to remove a joint holder from an account
/// </summary>
public class RemoveJointHolderRequest
{
    public Guid AccountId { get; set; }
    public Guid UserId { get; set; }
    public string? Reason { get; set; }
}

