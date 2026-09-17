namespace Bank.Application.DTOs.Account.Validation;

/// <summary>
/// Account number format validation result
/// </summary>
public class AccountNumberValidationResult
{
    public bool IsValid { get; set; }
    public string? FormattedAccountNumber { get; set; }
    public int ExpectedLength { get; set; }
    public int ActualLength { get; set; }
    public string? AccountType { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
}

