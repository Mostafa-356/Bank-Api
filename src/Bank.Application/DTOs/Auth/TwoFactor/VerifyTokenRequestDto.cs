namespace Bank.Application.DTOs.Auth.TwoFactor;

public class VerifyTokenRequest
{
    public string Token { get; set; } = string.Empty;
    
    public string? UserAgent { get; set; }
}

