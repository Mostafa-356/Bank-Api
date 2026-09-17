namespace Bank.Application.DTOs.Account.Validation;

/// <summary>
/// Routing number validation result
/// </summary>
public class RoutingNumberValidationResult
{
    public bool IsValid { get; set; }
    public string? BankName { get; set; }
    public string? BankAddress { get; set; }
    public string? FedwireParticipant { get; set; }
    public string? ACHParticipant { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
}

