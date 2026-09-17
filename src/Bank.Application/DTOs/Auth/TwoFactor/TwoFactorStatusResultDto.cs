using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Auth.TwoFactor;

/// <summary>
/// Current status and configuration of two-factor authentication for a user
/// </summary>
public class TwoFactorStatusResult
{
    public bool IsEnabled { get; set; }
    public TwoFactorStatus Status { get; set; }
    public List<TwoFactorMethod> EnabledMethods { get; set; } = new();
    public DateTime? SetupDate { get; set; }
    public DateTime? LastUsed { get; set; }
}

