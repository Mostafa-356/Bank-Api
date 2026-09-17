using Bank.Application.Interfaces;
using Bank.Application.Validators.Account;
using Bank.Application.Validators.Auth;
using Bank.Application.Validators.Card;
using Bank.Application.Validators.Payment;
using Bank.Application.Validators.Shared;

namespace Bank.Application.Services;

/// <summary>
/// Service implementation for validation operations
/// </summary>
public class ValidationService : IValidationService
{
    public bool ValidateIbanChecksum(string iban)
        => IbanValidator.ValidateChecksum(iban);

    public bool ValidateUSRoutingNumberChecksum(string routingNumber)
        => RoutingNumberValidator.ValidateChecksum(routingNumber);

    public bool PerformSanctionsCheck(string name, string? country = null)
        => SanctionsValidator.PerformCheck(name, country);

    public bool ValidateEmailFormat(string email)
        => EmailValidator.ValidateFormat(email);

    public bool ValidatePhoneNumberFormat(string phoneNumber)
        => PhoneNumberValidator.ValidateFormat(phoneNumber);

    public bool ValidateAccountNumberFormat(string accountNumber)
        => AccountNumberValidator.ValidateFormat(accountNumber);

    public bool ValidateCreditCardNumber(string cardNumber)
        => CreditCardValidator.ValidateNumber(cardNumber);

    public (bool IsValid, List<string> Errors) ValidatePasswordStrength(string password, int minLength = 8, 
        bool requireUppercase = true, bool requireLowercase = true, bool requireNumbers = true, bool requireSpecialChars = true)
        => PasswordValidator.ValidateStrength(password, minLength, requireUppercase, requireLowercase, requireNumbers, requireSpecialChars);

    public bool ValidatePinFormat(string pin, int expectedLength = 4)
        => PinValidator.ValidateFormat(pin, expectedLength);

    public bool ValidateAmountRange(decimal amount, decimal minAmount = 0.01m, decimal? maxAmount = null)
        => AmountValidator.ValidateRange(amount, minAmount, maxAmount);

    public bool ValidateCurrencyCode(string currencyCode)
        => CurrencyValidator.ValidateCode(currencyCode);
}