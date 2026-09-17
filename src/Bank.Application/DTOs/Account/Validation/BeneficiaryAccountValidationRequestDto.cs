using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Account.Validation;

/// <summary>
/// Request for comprehensive beneficiary account validation
/// </summary>
public class BeneficiaryAccountValidationRequest
{
    public string BeneficiaryName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string? SwiftCode { get; set; }
    public string? IbanNumber { get; set; }
    public string? RoutingNumber { get; set; }
    public string CountryCode { get; set; } = "US";
    public BeneficiaryType BeneficiaryType { get; set; }
    public bool PerformNameMatching { get; set; } = true;
    public bool CheckSanctionsList { get; set; } = true;
}

