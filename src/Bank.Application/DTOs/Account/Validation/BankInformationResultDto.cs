namespace Bank.Application.DTOs.Account.Validation;

/// <summary>
/// Bank information result
/// </summary>
public class BankInformationResult
{
    public bool Found { get; set; }
    public string? BankName { get; set; }
    public string? BankCode { get; set; }
    public string? SwiftCode { get; set; }
    public string? CountryCode { get; set; }
    public string? CountryName { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public List<string> SupportedCurrencies { get; set; } = new();
    public List<string> SupportedServices { get; set; } = new();
}

