using Bank.Application.DTOs.Common;

namespace Bank.Application.DTOs.Auth.TwoFactor;

/// <summary>
/// Result of a two-factor authentication token verification
/// </summary>
public class TwoFactorVerificationResult : BaseResultDto
{
    public bool RequiresAdditionalVerification { get; set; }
}


