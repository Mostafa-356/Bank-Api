namespace Bank.Application.DTOs.Account.JointAccount;

/// <summary>
/// Transaction permission check request
/// </summary>
public class TransactionPermissionRequest
{
    public Guid AccountId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
}

