using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Auth.Security;

public class IpWhitelistInfo
{
    public Guid Id { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string? IpRange { get; set; }
    public IpWhitelistType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string CreatedByUserName { get; set; } = string.Empty;
    public string? ApprovedByUserName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

