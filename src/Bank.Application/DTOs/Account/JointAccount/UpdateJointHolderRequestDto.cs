using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Account.JointAccount;

/// <summary>
/// Request to update joint holder role and permissions
/// </summary>
public class UpdateJointHolderRequest
{
    public Guid AccountId { get; set; }
    public Guid UserId { get; set; }
    public JointAccountRole NewRole { get; set; }
    public decimal? TransactionLimit { get; set; }
    public decimal? DailyLimit { get; set; }
    public bool RequiresSignature { get; set; }
    public string? Notes { get; set; }
}

