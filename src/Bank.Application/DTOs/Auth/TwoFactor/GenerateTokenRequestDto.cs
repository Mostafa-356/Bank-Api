using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Auth.TwoFactor;

public class GenerateTokenRequest
{
    public TwoFactorMethod Method { get; set; }
    public string? Destination { get; set; }
}

