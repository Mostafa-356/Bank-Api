using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Auth.Security;

public class PasswordPolicyInfo
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PasswordComplexityLevel ComplexityLevel { get; set; }
    public int MinimumLength { get; set; }
    public int MaximumLength { get; set; }
    public bool RequireUppercase { get; set; }
    public bool RequireLowercase { get; set; }
    public bool RequireDigits { get; set; }
    public bool RequireSpecialCharacters { get; set; }
    public int MinimumUniqueCharacters { get; set; }
    public int PasswordHistoryCount { get; set; }
    public TimeSpan MaxPasswordAge { get; set; }
    public int MaxFailedAttempts { get; set; }
    public TimeSpan LockoutDuration { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
}

