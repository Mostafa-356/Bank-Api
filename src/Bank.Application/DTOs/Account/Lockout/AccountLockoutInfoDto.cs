using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Account.Lockout;

public class AccountLockoutInfo
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int FailedAttempts { get; set; }
    public DateTime? LockedUntil { get; set; }
    public AccountLockoutReason? LockoutReason { get; set; }
    public bool IsCurrentlyLocked { get; set; }
    public string? LockoutNotes { get; set; }
    public DateTime? LastFailedAttempt { get; set; }
    public DateTime? LastSuccessfulLogin { get; set; }
    public string? LockedByUserName { get; set; }
    public DateTime CreatedAt { get; set; }
}

