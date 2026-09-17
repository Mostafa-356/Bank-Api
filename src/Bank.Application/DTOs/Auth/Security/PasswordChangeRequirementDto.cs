namespace Bank.Application.DTOs.Auth.Security;

public class PasswordChangeRequirement
{
    public bool IsRequired { get; set; }
    public DateTime? LastPasswordChange { get; set; }
    public DateTime? NextRequiredChange { get; set; }
    public TimeSpan? TimeUntilExpiry { get; set; }
    public string? Reason { get; set; }
}

