using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Auth.Security;

public class LockAccountRequest
{
    public AccountLockoutReason Reason { get; set; }
    public TimeSpan? LockoutDuration { get; set; }
    public string? Notes { get; set; }
}

