using Bank.Application.DTOs.Common;

namespace Bank.Application.DTOs.Auth.TwoFactor;

/// <summary>
/// Result of a two-factor authentication token generation request
/// </summary>
public class TwoFactorTokenResult : BaseResultDto
{
    public string? TokenId { get; set; }
    public DateTime? ExpiresAt { get; set; }
}


