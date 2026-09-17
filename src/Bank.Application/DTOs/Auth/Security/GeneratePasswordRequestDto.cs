using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Auth.Security;

public class GeneratePasswordRequest
{
    public PasswordComplexityLevel ComplexityLevel { get; set; } = PasswordComplexityLevel.Standard;
}

