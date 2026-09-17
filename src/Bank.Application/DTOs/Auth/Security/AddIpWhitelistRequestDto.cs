using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Auth.Security;

public class AddIpWhitelistRequest
{
    public string IpAddress { get; set; } = string.Empty;
    public IpWhitelistType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? IpRange { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

