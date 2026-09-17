using Bank.Application.DTOs;
using Bank.Application.Helpers.Account;
using Bank.Application.Interfaces;
using Bank.Domain.Enums;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Bank.Application.Services;

/// <summary>
/// Service for validating external bank accounts and international banking codes
/// </summary>
public sealed class AccountValidationService : IAccountValidationService
{
    private readonly ILogger<AccountValidationService> _logger;
    private readonly IAuditLogService _auditLogService;

    // Regex timeout to prevent ReDoS attacks
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(500);

    // SWIFT code pattern: 4 letters (bank) + 2 letters (country) + 2 alphanumeric (location) + optional 3 alphanumeric (branch)
    private static readonly Regex SwiftCodePattern = new(@"^[A-Z]{4}[A-Z]{2}[A-Z0-9]{2}([A-Z0-9]{3})?$", RegexOptions.Compiled, RegexTimeout);
    
    // Default branch code when not specified in SWIFT code
    private const string DefaultBranchCode = "XXX";
    
    // IBAN patterns by country (simplified - in production, use comprehensive IBAN registry)
    private static readonly Dictionary<string, (int Length, Regex Pattern)> IbanPatterns = new()
    {
        { "AD", (24, new Regex(@"^AD\d{2}\d{4}\d{4}\d{12}$", RegexOptions.None, RegexTimeout)) },
        { "AE", (23, new Regex(@"^AE\d{2}\d{3}\d{16}$", RegexOptions.None, RegexTimeout)) },
        { "AL", (28, new Regex(@"^AL\d{2}\d{8}[A-Z0-9]{16}$", RegexOptions.None, RegexTimeout)) },
        { "AT", (20, new Regex(@"^AT\d{2}\d{5}\d{11}$", RegexOptions.None, RegexTimeout)) },
        { "BE", (16, new Regex(@"^BE\d{2}\d{3}\d{7}\d{2}$", RegexOptions.None, RegexTimeout)) },
        { "BG", (22, new Regex(@"^BG\d{2}[A-Z]{4}\d{6}[A-Z0-9]{8}$", RegexOptions.None, RegexTimeout)) },
        { "CH", (21, new Regex(@"^CH\d{2}\d{5}[A-Z0-9]{12}$", RegexOptions.None, RegexTimeout)) },
        { "DE", (22, new Regex(@"^DE\d{2}\d{8}\d{10}$", RegexOptions.None, RegexTimeout)) },
        { "ES", (24, new Regex(@"^ES\d{2}\d{4}\d{4}\d{1}\d{1}\d{10}$", RegexOptions.None, RegexTimeout)) },
        { "FR", (27, new Regex(@"^FR\d{2}\d{5}\d{5}[A-Z0-9]{11}\d{2}$", RegexOptions.None, RegexTimeout)) },
        { "GB", (22, new Regex(@"^GB\d{2}[A-Z]{4}\d{6}\d{8}$", RegexOptions.None, RegexTimeout)) },
        { "IT", (27, new Regex(@"^IT\d{2}[A-Z]{1}\d{5}\d{5}[A-Z0-9]{12}$", RegexOptions.None, RegexTimeout)) },
        { "NL", (18, new Regex(@"^NL\d{2}[A-Z]{4}\d{10}$", RegexOptions.None, RegexTimeout)) },
        { "US", (0, new Regex(@"^$", RegexOptions.None, RegexTimeout)) } // US doesn't use IBAN
    };

    public AccountValidationService(
        ILogger<AccountValidationService> logger,
        IAuditLogService auditLogService)
    {
        _logger = logger;
        _auditLogService = auditLogService;
    }

    public async Task<AccountValidationResult> ValidateExternalAccountAsync(ExternalAccountValidationRequest request)
    {
        try
        {
            var result = new AccountValidationResult
            {
                ValidationReference = Guid.NewGuid().ToString("N")[..8].ToUpper()
            };

            var errors = new List<string>();

            // Basic validation
            if (string.IsNullOrEmpty(request.AccountNumber))
                errors.Add("Account number is required");

            if (string.IsNullOrEmpty(request.BankCode))
                errors.Add("Bank code is required");

            // Type-specific validation
            if (request.BeneficiaryType == BeneficiaryType.International)
            {
                if (string.IsNullOrEmpty(request.SwiftCode) && string.IsNullOrEmpty(request.IbanNumber))
                    errors.Add("SWIFT code or IBAN is required for international transfers");
            }

            if (request.BeneficiaryType == BeneficiaryType.External && string.IsNullOrEmpty(request.RoutingNumber))
            {
                errors.Add("Routing number is required for external transfers");
            }

            // Simulate external bank API call for account verification
            if (errors.Count == 0)
            {
                var accountExists = await SimulateExternalBankApiCall(request);
                if (accountExists)
                {
                    result.IsValid = true;
                    result.AccountHolderName = "External Account Holder"; // Would come from API
                    result.BankName = await GetBankNameFromCode(request.BankCode);
                    result.BankAddress = "Bank Address"; // Would come from API
                }
                else
                {
                    errors.Add("Account not found or invalid");
                }
            }

            result.ValidationErrors = errors;
            result.IsValid = errors.Count == 0;

            await _auditLogService.LogAsync("External Account Validation", 
                $"Validated account {MaskAccountNumber(request.AccountNumber)} at bank {request.BankCode}. Result: {result.IsValid}");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating external account {AccountNumberMasked}", MaskAccountNumber(request.AccountNumber));
            return new AccountValidationResult
            {
                IsValid = false,
                ValidationErrors = new List<string> { "Validation service temporarily unavailable" }
            };
        }
    }

    public async Task<SwiftValidationResult> ValidateSwiftCodeAsync(string swiftCode)
    {
        try
        {
            var result = new SwiftValidationResult();

            if (string.IsNullOrEmpty(swiftCode))
            {
                result.ValidationErrors.Add("SWIFT code is required");
                return result;
            }

            swiftCode = swiftCode.ToUpper().Replace(" ", "");

            if (!SwiftCodePattern.IsMatch(swiftCode))
            {
                result.ValidationErrors.Add("Invalid SWIFT code format");
                return result;
            }

            // Parse SWIFT code components
            result.BankCode = swiftCode[..4];
            result.CountryCode = swiftCode.Substring(4, 2);
            result.LocationCode = swiftCode.Substring(6, 2);
            result.BranchCode = swiftCode.Length > 8 ? swiftCode.Substring(8, 3) : DefaultBranchCode;

            // Simulate SWIFT directory lookup
            var bankInfo = await SimulateSwiftDirectoryLookup(swiftCode);
            if (bankInfo != null)
            {
                result.IsValid = true;
                result.IsActive = true;
                result.BankName = bankInfo.BankName;
                result.BankAddress = bankInfo.Address;
            }
            else
            {
                result.ValidationErrors.Add("SWIFT code not found in directory");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating SWIFT code {SwiftCode}", swiftCode);
            return new SwiftValidationResult
            {
                IsValid = false,
                ValidationErrors = new List<string> { "SWIFT validation service temporarily unavailable" }
            };
        }
    }

    public async Task<IbanValidationResult> ValidateIbanAsync(string iban)
    {
        try
        {
            var result = new IbanValidationResult();

            if (string.IsNullOrEmpty(iban))
            {
                result.ValidationErrors.Add("IBAN is required");
                return result;
            }

            iban = iban.ToUpper().Replace(" ", "");
            result.IbanLength = iban.Length;

            if (iban.Length < 15 || iban.Length > 34)
            {
                result.ValidationErrors.Add("IBAN length must be between 15 and 34 characters");
                return result;
            }

            // Extract country code and check digits
            result.CountryCode = iban[..2];
            result.CheckDigits = iban.Substring(2, 2);

            // Validate country-specific format
            if (IbanPatterns.TryGetValue(result.CountryCode, out var pattern))
            {
                if (pattern.Length > 0 && iban.Length != pattern.Length)
                {
                    result.ValidationErrors.Add($"Invalid IBAN length for {result.CountryCode}. Expected {pattern.Length}, got {iban.Length}");
                    return result;
                }

                if (!pattern.Pattern.IsMatch(iban))
                {
                    result.ValidationErrors.Add($"Invalid IBAN format for {result.CountryCode}");
                    return result;
                }
            }
            else
            {
                result.ValidationErrors.Add($"Unsupported country code: {result.CountryCode}");
                return result;
            }

            // Validate checksum using mod-97 algorithm
            result.ChecksumValid = BankingCodeValidationHelper.ValidateIbanChecksum(iban);
            if (!result.ChecksumValid)
            {
                result.ValidationErrors.Add("Invalid IBAN checksum");
                return result;
            }

            // Extract bank code and account number (country-specific)
            var (bankCode, accountNumber) = BankingCodeValidationHelper.ExtractIbanComponents(iban, result.CountryCode ?? "");
            result.BankCode = bankCode;
            result.AccountNumber = accountNumber;

            result.IsValid = result.ValidationErrors.Count == 0;

            await Task.CompletedTask; // Simulate async operation
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating IBAN {Iban}", iban);
            return new IbanValidationResult
            {
                IsValid = false,
                ValidationErrors = new List<string> { "IBAN validation service temporarily unavailable" }
            };
        }
    }

    public async Task<RoutingNumberValidationResult> ValidateRoutingNumberAsync(string routingNumber, string countryCode = "US")
    {
        try
        {
            var result = new RoutingNumberValidationResult();

            if (string.IsNullOrEmpty(routingNumber))
            {
                result.ValidationErrors.Add("Routing number is required");
                return result;
            }

            routingNumber = routingNumber.Replace("-", "").Replace(" ", "");

            if (countryCode == "US")
            {
                // US routing number validation (9 digits with checksum)
                if (routingNumber.Length != 9 || !routingNumber.All(char.IsDigit))
                {
                    result.ValidationErrors.Add("US routing number must be 9 digits");
                    return result;
                }

                if (!BankingCodeValidationHelper.ValidateUSRoutingNumberChecksum(routingNumber))
                {
                    result.ValidationErrors.Add("Invalid routing number checksum");
                    return result;
                }

                // Simulate Federal Reserve lookup
                var bankInfo = await SimulateFedLookup(routingNumber);
                if (bankInfo != null)
                {
                    result.IsValid = true;
                    result.BankName = bankInfo.BankName;
                    result.BankAddress = bankInfo.Address;
                    result.FedwireParticipant = "Yes";
                    result.ACHParticipant = "Yes";
                }
                else
                {
                    result.ValidationErrors.Add("Routing number not found");
                }
            }
            else
            {
                result.ValidationErrors.Add($"Routing number validation not supported for country: {countryCode}");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating routing number {RoutingNumber}", routingNumber);
            return new RoutingNumberValidationResult
            {
                IsValid = false,
                ValidationErrors = new List<string> { "Routing number validation service temporarily unavailable" }
            };
        }
    }

    public async Task<BankInformationResult> GetBankInformationAsync(string swiftCode)
    {
        try
        {
            var result = new BankInformationResult();

            var swiftValidation = await ValidateSwiftCodeAsync(swiftCode);
            if (!swiftValidation.IsValid)
            {
                return result;
            }

            // Simulate comprehensive bank information lookup
            var bankInfo = await SimulateSwiftDirectoryLookup(swiftCode);
            if (bankInfo != null)
            {
                result.Found = true;
                result.BankName = bankInfo.BankName;
                result.BankCode = swiftValidation.BankCode;
                result.SwiftCode = swiftCode;
                result.CountryCode = swiftValidation.CountryCode;
                result.CountryName = GetCountryName(swiftValidation.CountryCode);
                result.City = bankInfo.City;
                result.Address = bankInfo.Address;
                result.SupportedCurrencies = GetSupportedCurrencies(swiftValidation.CountryCode);
                result.SupportedServices = new List<string> { "Wire Transfer", "SWIFT MT103", "Correspondent Banking" };
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting bank information for SWIFT {SwiftCode}", swiftCode);
            return new BankInformationResult { Found = false };
        }
    }

    public async Task<AccountNumberValidationResult> ValidateAccountNumberFormatAsync(string accountNumber, string bankCode, string countryCode)
    {
        try
        {
            var result = new AccountNumberValidationResult
            {
                ActualLength = accountNumber.Length
            };

            // Country and bank-specific account number validation
            switch (countryCode.ToUpper())
            {
                case "US":
                    result.ExpectedLength = 12; // Typical US account number length
                    result.IsValid = accountNumber.Length >= 8 && accountNumber.Length <= 17 && accountNumber.All(char.IsDigit);
                    result.AccountType = "Checking/Savings";
                    break;

                case "GB":
                    result.ExpectedLength = 8;
                    result.IsValid = accountNumber.Length == 8 && accountNumber.All(char.IsDigit);
                    result.AccountType = "UK Account";
                    break;

                case "DE":
                    result.ExpectedLength = 10;
                    result.IsValid = accountNumber.Length == 10 && accountNumber.All(char.IsDigit);
                    result.AccountType = "German Account";
                    break;

                default:
                    result.IsValid = accountNumber.Length >= 8 && accountNumber.Length <= 20;
                    result.AccountType = "International";
                    break;
            }

            if (!result.IsValid)
            {
                result.ValidationErrors.Add($"Invalid account number format for {countryCode}");
            }
            else
            {
                result.FormattedAccountNumber = FormatAccountNumber(accountNumber, countryCode);
            }

            await Task.CompletedTask;
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating account number format");
            return new AccountNumberValidationResult
            {
                IsValid = false,
                ValidationErrors = new List<string> { "Account number validation service temporarily unavailable" }
            };
        }
    }

    public async Task<ComprehensiveValidationResult> ValidateBeneficiaryAccountAsync(BeneficiaryAccountValidationRequest request)
    {
        try
        {
            var result = new ComprehensiveValidationResult
            {
                ValidationReference = Guid.NewGuid().ToString("N")[..8].ToUpper()
            };

            var errors = new List<string>();
            var warnings = new List<string>();

            // Perform all validations
            await PerformBankingCodeValidations(request, result, errors);
            await PerformAccountValidation(request, result, errors);
            PerformNameValidation(request, result, warnings);
            await PerformSanctionsValidation(request, result, errors);

            // Set bank name from successful validation
            result.BankName = result.SwiftValidation?.BankName ?? 
                             result.RoutingValidation?.BankName ?? 
                             result.AccountValidation?.BankName;

            result.ValidationErrors = errors;
            result.Warnings = warnings;
            result.IsValid = errors.Count == 0;

            // Generate validation summary
            result.ValidationSummary = GenerateValidationSummary(result);

            await _auditLogService.LogAsync("Comprehensive Beneficiary Validation", 
                $"Validated beneficiary account {MaskAccountNumber(request.AccountNumber)}. Result: {result.IsValid}");

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error performing comprehensive beneficiary validation");
            return new ComprehensiveValidationResult
            {
                IsValid = false,
                ValidationErrors = new List<string> { "Comprehensive validation service temporarily unavailable" }
            };
        }
    }

    private async Task PerformBankingCodeValidations(BeneficiaryAccountValidationRequest request, ComprehensiveValidationResult result, List<string> errors)
    {
        // Validate SWIFT code
        if (!string.IsNullOrEmpty(request.SwiftCode))
        {
            result.SwiftValidation = await ValidateSwiftCodeAsync(request.SwiftCode);
            if (!result.SwiftValidation.IsValid)
                errors.AddRange(result.SwiftValidation.ValidationErrors.Select(e => $"SWIFT: {e}"));
        }

        // Validate IBAN
        if (!string.IsNullOrEmpty(request.IbanNumber))
        {
            result.IbanValidation = await ValidateIbanAsync(request.IbanNumber);
            if (!result.IbanValidation.IsValid)
                errors.AddRange(result.IbanValidation.ValidationErrors.Select(e => $"IBAN: {e}"));
        }

        // Validate Routing number
        if (!string.IsNullOrEmpty(request.RoutingNumber))
        {
            result.RoutingValidation = await ValidateRoutingNumberAsync(request.RoutingNumber, request.CountryCode);
            if (!result.RoutingValidation.IsValid)
                errors.AddRange(result.RoutingValidation.ValidationErrors.Select(e => $"Routing: {e}"));
        }
    }

    private async Task PerformAccountValidation(BeneficiaryAccountValidationRequest request, ComprehensiveValidationResult result, List<string> errors)
    {
        var accountRequest = new ExternalAccountValidationRequest
        {
            AccountNumber = request.AccountNumber,
            BankCode = request.BankCode,
            SwiftCode = request.SwiftCode,
            IbanNumber = request.IbanNumber,
            RoutingNumber = request.RoutingNumber,
            CountryCode = request.CountryCode,
            BeneficiaryType = request.BeneficiaryType
        };

        result.AccountValidation = await ValidateExternalAccountAsync(accountRequest);
        result.AccountExists = result.AccountValidation.IsValid;

        if (!result.AccountExists)
            errors.AddRange(result.AccountValidation.ValidationErrors.Select(e => $"Account: {e}"));
    }

    private void PerformNameValidation(BeneficiaryAccountValidationRequest request, ComprehensiveValidationResult result, List<string> warnings)
    {
        if (!request.PerformNameMatching || !result.AccountExists)
            return;

        result.NameMatches = PerformNameMatching(request.BeneficiaryName, result.AccountValidation.AccountHolderName);
        result.MatchedAccountHolderName = result.AccountValidation.AccountHolderName;
        
        if (!result.NameMatches)
            warnings.Add("Beneficiary name does not match account holder name");
    }

    private async Task PerformSanctionsValidation(BeneficiaryAccountValidationRequest request, ComprehensiveValidationResult result, List<string> errors)
    {
        if (!request.CheckSanctionsList)
            return;

        result.PassesSanctionsCheck = await PerformSanctionsCheck(request.BeneficiaryName, request.SwiftCode);
        if (!result.PassesSanctionsCheck)
            errors.Add("Beneficiary appears on sanctions list");
    }

    #region Private Helper Methods

    /// <summary>
    /// Masks an account number for safe logging — shows only the last 4 digits.
    /// e.g. "1234567890" → "******7890"
    /// </summary>
    private static string MaskAccountNumber(string? accountNumber)
    {
        if (string.IsNullOrEmpty(accountNumber)) return "[empty]";
        if (accountNumber.Length <= 4) return "[redacted]";
        return $"{"*".PadRight(accountNumber.Length - 4, '*')}{accountNumber[^4..]}";
    }

    private static async Task<bool> SimulateExternalBankApiCall(ExternalAccountValidationRequest request)
    {
        // Simulate API call delay
        await Task.Delay(100);
        
        // For demo purposes, return true for valid-looking account numbers
        return request.AccountNumber.Length >= 8 && request.AccountNumber.All(char.IsDigit);
    }

    private static async Task<BankInformationResult?> SimulateSwiftDirectoryLookup(string swiftCode)
    {
        await Task.Delay(50);
        
        // Simulate SWIFT directory with some known codes
        var mockBanks = new Dictionary<string, BankInformationResult>
        {
            { "CHASUS33", new BankInformationResult { BankName = "JPMorgan Chase Bank", City = "New York", Address = "270 Park Avenue, New York, NY" } },
            { "BOFAUS3N", new BankInformationResult { BankName = "Bank of America", City = "Charlotte", Address = "100 N Tryon St, Charlotte, NC" } },
            { "CITIUS33", new BankInformationResult { BankName = "Citibank", City = "New York", Address = "388 Greenwich St, New York, NY" } },
            { "DEUTDEFF", new BankInformationResult { BankName = "Deutsche Bank AG", City = "Frankfurt", Address = "Taunusanlage 12, Frankfurt am Main" } }
        };

        return mockBanks.TryGetValue(swiftCode, out var bank) ? bank : null;
    }

    private static async Task<BankInformationResult?> SimulateFedLookup(string routingNumber)
    {
        await Task.Delay(50);
        
        // Simulate Federal Reserve lookup with some known routing numbers
        var mockBanks = new Dictionary<string, BankInformationResult>
        {
            { "021000021", new BankInformationResult { BankName = "JPMorgan Chase Bank", Address = "270 Park Avenue, New York, NY" } },
            { "026009593", new BankInformationResult { BankName = "Bank of America", Address = "100 N Tryon St, Charlotte, NC" } },
            { "021000089", new BankInformationResult { BankName = "Citibank", Address = "388 Greenwich St, New York, NY" } }
        };

        return mockBanks.TryGetValue(routingNumber, out var bank) ? bank : null;
    }

    private static async Task<string> GetBankNameFromCode(string bankCode)
    {
        await Task.Delay(10);
        return $"Bank {bankCode}"; // Simplified for demo
    }

    private static string GetCountryName(string countryCode)
    {
        var countries = new Dictionary<string, string>
        {
            { "US", "United States" },
            { "GB", "United Kingdom" },
            { "DE", "Germany" },
            { "FR", "France" },
            { "IT", "Italy" },
            { "ES", "Spain" },
            { "NL", "Netherlands" },
            { "CH", "Switzerland" },
            { "AT", "Austria" },
            { "BE", "Belgium" }
        };

        return countries.TryGetValue(countryCode, out var name) ? name : countryCode;
    }

    private static List<string> GetSupportedCurrencies(string countryCode)
    {
        var currencies = new Dictionary<string, List<string>>
        {
            { "US", new List<string> { "USD" } },
            { "GB", new List<string> { "GBP", "USD", "EUR" } },
            { "DE", new List<string> { "EUR", "USD" } },
            { "FR", new List<string> { "EUR", "USD" } },
            { "CH", new List<string> { "CHF", "EUR", "USD" } }
        };

        return currencies.TryGetValue(countryCode, out var currencyList) ? currencyList : new List<string> { "USD" };
    }

    private static string FormatAccountNumber(string accountNumber, string countryCode)
    {
        return countryCode.ToUpper() switch
        {
            "US" => accountNumber.Length > 8 ? $"{accountNumber[..4]}-{accountNumber[4..8]}-{accountNumber[8..]}" : accountNumber,
            "GB" => accountNumber.Length == 8 ? $"{accountNumber[..2]}-{accountNumber[2..4]}-{accountNumber[4..]}" : accountNumber,
            "DE" => accountNumber.Length == 10 ? $"{accountNumber[..3]} {accountNumber[3..6]} {accountNumber[6..]}" : accountNumber,
            _ => accountNumber
        };
    }

    private static bool PerformNameMatching(string beneficiaryName, string? accountHolderName)
    {
        if (string.IsNullOrEmpty(accountHolderName))
            return false;

        // Simple name matching - in production, use fuzzy matching algorithms
        var normalizedBeneficiary = beneficiaryName.ToLower().Replace(" ", "").Replace(".", "");
        var normalizedAccount = accountHolderName.ToLower().Replace(" ", "").Replace(".", "");

        return normalizedBeneficiary.Contains(normalizedAccount) || normalizedAccount.Contains(normalizedBeneficiary);
    }

    private static async Task<bool> PerformSanctionsCheck(string beneficiaryName, string? swiftCode)
    {
        await Task.Delay(50); // Simulate sanctions database lookup
        
        // Simplified sanctions check - in production, integrate with OFAC, UN, EU sanctions lists
        var sanctionedNames = new[] { "sanctioned", "blocked", "prohibited" };
        var sanctionedSwiftCodes = new[] { "SANCTXXX", "BLOCXXX" }; // Known sanctioned SWIFT codes

        var nameSanctioned = sanctionedNames.Any(s => beneficiaryName.ToLower().Contains(s));
        var swiftSanctioned = !string.IsNullOrEmpty(swiftCode) && sanctionedSwiftCodes.Contains(swiftCode.ToUpper());

        return !nameSanctioned && !swiftSanctioned;
    }

    private static string GenerateValidationSummary(ComprehensiveValidationResult result)
    {
        var summary = new List<string>();

        if (result.AccountExists)
            summary.Add("✓ Account exists");
        else
            summary.Add("✗ Account not found");

        if (result.SwiftValidation?.IsValid == true)
            summary.Add("✓ SWIFT code valid");
        else if (result.SwiftValidation != null)
            summary.Add("✗ SWIFT code invalid");

        if (result.IbanValidation?.IsValid == true)
            summary.Add("✓ IBAN valid");
        else if (result.IbanValidation != null)
            summary.Add("✗ IBAN invalid");

        if (result.RoutingValidation?.IsValid == true)
            summary.Add("✓ Routing number valid");
        else if (result.RoutingValidation != null)
            summary.Add("✗ Routing number invalid");

        if (result.NameMatches)
            summary.Add("✓ Name matches");
        else if (result.MatchedAccountHolderName != null)
            summary.Add("⚠ Name mismatch");

        if (result.PassesSanctionsCheck)
            summary.Add("✓ Sanctions check passed");
        else
            summary.Add("✗ Sanctions check failed");

        return string.Join(", ", summary);
    }

    #endregion
}