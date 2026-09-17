using Bank.Application.DTOs;
using Bank.Application.DTOs.Auth.TwoFactor;
using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Microsoft.AspNetCore.Identity;

namespace Bank.Infrastructure.Services.Security;

/// <summary>
/// Implementation of two-factor authentication using ASP.NET Core Identity.
/// </summary>
public class TwoFactorService : ITwoFactorAuthService
{
    private readonly UserManager<User> _userManager;

    public TwoFactorService(UserManager<User> userManager)
    {
        _userManager = userManager;
    }

    public Task<TwoFactorTokenResult> GenerateTokenAsync(Guid userId, TwoFactorMethod method, string? destination = null)
    {
        // For TOTP, token generation for login is handled by Identity's SignInManager typically.
        // If needed for manual sending (like SMS/Email):
        throw new NotImplementedException("SMS/Email 2FA not implemented. Use Authenticator App.");
    }

    public async Task<TwoFactorVerificationResult> VerifyTokenAsync(Guid userId, string token, string? ipAddress = null, string? userAgent = null)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return new TwoFactorVerificationResult { Success = false, Message = "User not found" };

        // Verify TOTP token
        var isValid = await _userManager.VerifyTwoFactorTokenAsync(user, _userManager.Options.Tokens.AuthenticatorTokenProvider, token);
        
        return new TwoFactorVerificationResult
        {
            Success = isValid,
            Message = isValid ? "Token verified" : "Invalid token"
        };
    }

    public async Task<TwoFactorSetupResult> SetupAuthenticatorAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return new TwoFactorSetupResult { Success = false, Message = "User not found" };

        var unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        var email = await _userManager.GetEmailAsync(user);
        // Using AuthGeneratorHelper equivalent logic for QR code URL formatting.
        // Assuming issuer is "Bank-Api"
        var qrCodeUrl = string.Format(
            "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6",
            Uri.EscapeDataString("BankSimulator"),
            Uri.EscapeDataString(email ?? user.UserName ?? "User"),
            unformattedKey);

        return new TwoFactorSetupResult
        {
            Success = true,
            SecretKey = unformattedKey,
            QrCodeUrl = qrCodeUrl,
            Message = "Authenticator setup initialized"
        };
    }

    public async Task<TwoFactorSetupResult> CompleteSetupAsync(Guid userId, string verificationToken)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return new TwoFactorSetupResult { Success = false, Message = "User not found" };

        var is2faTokenValid = await _userManager.VerifyTwoFactorTokenAsync(user, _userManager.Options.Tokens.AuthenticatorTokenProvider, verificationToken);
        if (!is2faTokenValid)
            return new TwoFactorSetupResult { Success = false, Message = "Verification code is invalid." };

        await _userManager.SetTwoFactorEnabledAsync(user, true);

        // Generate backup codes
        var backupCodes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10) ?? new List<string>();

        return new TwoFactorSetupResult
        {
            Success = true,
            Message = "Two-factor authentication has been enabled.",
            BackupCodes = backupCodes.ToList()
        };
    }

    public async Task<bool> DisableTwoFactorAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return false;

        var result = await _userManager.SetTwoFactorEnabledAsync(user, false);
        return result.Succeeded;
    }

    public async Task<List<string>> GenerateBackupCodesAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return new List<string>();

        var codes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        return codes?.ToList() ?? new List<string>();
    }

    public async Task<bool> VerifyBackupCodeAsync(Guid userId, string backupCode)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return false;

        var result = await _userManager.RedeemTwoFactorRecoveryCodeAsync(user, backupCode);
        return result.Succeeded;
    }

    public async Task<bool> IsTwoFactorEnabledAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return false;

        return await _userManager.GetTwoFactorEnabledAsync(user);
    }

    public async Task<TwoFactorStatusResult> GetTwoFactorStatusAsync(Guid userId)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return new TwoFactorStatusResult { IsEnabled = false, Status = TwoFactorStatus.NotSetup };

        var isEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
        var methods = new List<TwoFactorMethod>();
        if (isEnabled)
        {
            methods.Add(TwoFactorMethod.AuthenticatorApp); // TOTP is default
        }

        return new TwoFactorStatusResult
        {
            IsEnabled = isEnabled,
            Status = isEnabled ? TwoFactorStatus.Active : TwoFactorStatus.Disabled,
            EnabledMethods = methods
        };
    }
}
