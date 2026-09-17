namespace Bank.Application.DTOs.Auth.TwoFactor;

public class CompleteSetupRequest
{
    public string VerificationToken { get; set; } = string.Empty;
}

