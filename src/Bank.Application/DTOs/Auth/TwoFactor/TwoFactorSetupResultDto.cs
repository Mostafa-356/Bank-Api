using Bank.Application.DTOs.Common;

namespace Bank.Application.DTOs.Auth.TwoFactor;

/// <summary>
/// Result of two-factor authentication setup operations
/// </summary>
public class TwoFactorSetupResult : BaseResultDto
{
    public string? SecretKey { get; set; }
    public string? QrCodeUrl { get; set; }
    public List<string>? BackupCodes { get; set; }
}


