using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Account.Lockout;

public class LockoutStatistics
{
    public int TotalLockedAccounts { get; set; }
    public int AccountsLockedToday { get; set; }
    public int AccountsUnlockedToday { get; set; }
    public Dictionary<AccountLockoutReason, int> LockoutsByReason { get; set; } = new();
    public int ExpiredLockoutsCleanedUp { get; set; }
    public DateTime LastCleanupAt { get; set; }
}

