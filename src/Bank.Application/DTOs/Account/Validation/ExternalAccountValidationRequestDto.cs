using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Account.Validation;

/// <summary>
/// Request for external account validation
/// </summary>
public class ExternalAccountValidationRequest
{
    public string AccountNumber { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string? SwiftCode { get; set; }
    public string? IbanNumber { get; set; }
    public string? RoutingNumber { get; set; }
    public string CountryCode { get; set; } = "US";
    public BeneficiaryType BeneficiaryType { get; set; }
}

