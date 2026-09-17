namespace Bank.Application.DTOs.Auth.TwoFactor;

public class VerifyBackupCodeRequest
{
    public string BackupCode { get; set; } = string.Empty;
}

