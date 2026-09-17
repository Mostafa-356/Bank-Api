namespace Bank.Application.DTOs.Account.Validation;

/// <summary>
/// Result of account validation
/// </summary>
public class AccountValidationResult
{
    public bool IsValid { get; set; }
    public string? AccountHolderName { get; set; }
    public string? BankName { get; set; }
    public string? BankAddress { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public DateTime ValidationDate { get; set; } = DateTime.UtcNow;
    public string? ValidationReference { get; set; }
}

