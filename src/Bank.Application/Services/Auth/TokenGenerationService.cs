using Bank.Application.Interfaces;
using Bank.Application.Helpers.Auth;
using Bank.Application.Helpers.Shared;

namespace Bank.Application.Services;

/// <summary>
/// Service implementation for token generation
/// </summary>
public class TokenGenerationService : ITokenGenerationService
{
    public string GenerateSecureToken(int length = 32)
        => AuthGeneratorHelper.GenerateSecureToken(length);

    public string GenerateNumericToken(int length = 6)
        => AuthGeneratorHelper.GenerateNumericToken(length);

    public string GenerateActivationCode(int length = 8)
        => AuthGeneratorHelper.GenerateActivationCode(length);

    public string GenerateRandomPin(int length = 4)
        => AuthGeneratorHelper.GenerateRandomPin(length);

    public string GenerateConfirmationNumber(string prefix = "CNF")
        => TokenHelper.GeneratePaymentConfirmationNumber(prefix);

    public List<string> GenerateBackupCodes(int count = 10, int length = 8)
        => AuthGeneratorHelper.GenerateBackupCodes(count, length);

    public string GenerateSecretKey(int length = 20)
        => AuthGeneratorHelper.GenerateSecretKey(length);

    public string GenerateQrCodeUrl(string issuer, string accountName, string secretKey)
        => AuthGeneratorHelper.GenerateQrCodeUrl(issuer, accountName, secretKey);

    public string GeneratePassword(int length = 12, bool includeUppercase = true, 
        bool includeLowercase = true, bool includeNumbers = true, bool includeSpecialChars = true)
        => AuthGeneratorHelper.GeneratePassword(length, includeUppercase, includeLowercase, includeNumbers, includeSpecialChars);

    public string GenerateRandomCode(int length = 6, bool alphanumeric = false)
        => AuthGeneratorHelper.GenerateRandomCode(length, alphanumeric);
}