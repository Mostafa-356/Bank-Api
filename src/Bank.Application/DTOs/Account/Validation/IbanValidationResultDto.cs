namespace Bank.Application.DTOs.Account.Validation;

/// <summary>
/// IBAN validation result
/// </summary>
public class IbanValidationResult
{
    public bool IsValid { get; set; }
    public string? CountryCode { get; set; }
    public string? CheckDigits { get; set; }
    public string? BankCode { get; set; }
    public string? AccountNumber { get; set; }
    public int IbanLength { get; set; }
    public bool ChecksumValid { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
}

