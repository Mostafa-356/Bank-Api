namespace Bank.Application.DTOs.Auth.Security;

public class IpWhitelistResult
{
    public bool Success { get; set; }
    public Guid? WhitelistId { get; set; }
    public string? ErrorMessage { get; set; }
    public bool RequiresApproval { get; set; }
}

