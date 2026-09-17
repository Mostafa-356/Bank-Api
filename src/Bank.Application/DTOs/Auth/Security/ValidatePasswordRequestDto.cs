using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Auth.Security;

public class ValidatePasswordRequest
{
    public string Password { get; set; } = string.Empty;
    public PasswordComplexityLevel? ComplexityLevel { get; set; }
}

