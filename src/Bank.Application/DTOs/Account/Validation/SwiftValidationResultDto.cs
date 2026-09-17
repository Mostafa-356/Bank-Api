namespace Bank.Application.DTOs.Account.Validation;

/// <summary>
/// SWIFT code validation result
/// </summary>
public class SwiftValidationResult
{
    public bool IsValid { get; set; }
    public string? BankName { get; set; }
    public string? BankCode { get; set; }
    public string? CountryCode { get; set; }
    public string? LocationCode { get; set; }
    public string? BranchCode { get; set; }
    public string? BankAddress { get; set; }
    public bool IsActive { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
}

