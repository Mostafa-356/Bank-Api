using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Account.Lockout;

public class LockoutResult
{
    public bool IsLocked { get; set; }
    public int FailedAttempts { get; set; }
    public int MaxFailedAttempts { get; set; }
    public TimeSpan? RemainingLockoutTime { get; set; }
    public AccountLockoutReason? LockoutReason { get; set; }
    public string? Message { get; set; }
    public bool ShouldLockAccount { get; set; }
}

