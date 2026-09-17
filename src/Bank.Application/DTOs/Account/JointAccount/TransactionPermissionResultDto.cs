namespace Bank.Application.DTOs.Account.JointAccount;

/// <summary>
/// Transaction permission result
/// </summary>
public class TransactionPermissionResult
{
    public bool CanPerformTransaction { get; set; }
    public bool RequiresMultipleSignatures { get; set; }
    public int SignaturesRequired { get; set; }
    public string? RestrictionReason { get; set; }
    public decimal? UserTransactionLimit { get; set; }
    public decimal? UserDailyLimit { get; set; }
}

