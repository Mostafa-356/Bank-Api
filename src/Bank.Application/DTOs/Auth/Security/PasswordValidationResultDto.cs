using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Auth.Security;

public class PasswordValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public PasswordComplexityLevel RequiredComplexityLevel { get; set; }
    public bool IsPasswordRecentlyUsed { get; set; }
    public bool IsCommonPassword { get; set; }
    public bool ContainsUserInfo { get; set; }
    public int PasswordStrengthScore { get; set; }
}

