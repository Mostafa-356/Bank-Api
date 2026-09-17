namespace Bank.Application.DTOs.Account.Validation;

/// <summary>
/// Comprehensive validation result
/// </summary>
public class ComprehensiveValidationResult
{
    public bool IsValid { get; set; }
    public bool AccountExists { get; set; }
    public bool NameMatches { get; set; }
    public bool PassesSanctionsCheck { get; set; }
    public string? MatchedAccountHolderName { get; set; }
    public string? BankName { get; set; }
    public AccountValidationResult? AccountValidation { get; set; }
    public SwiftValidationResult? SwiftValidation { get; set; }
    public IbanValidationResult? IbanValidation { get; set; }
    public RoutingNumberValidationResult? RoutingValidation { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string ValidationSummary { get; set; } = string.Empty;
    public DateTime ValidationDate { get; set; } = DateTime.UtcNow;
    public string? ValidationReference { get; set; }
}

