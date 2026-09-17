using Bank.Application.DTOs;
using Bank.Application.Interfaces;
using Bank.Application.Helpers;
using Bank.Application.Helpers.Shared;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace Bank.Application.Services;

/// <summary>
/// Service for managing card PIN operations
/// </summary>
public class PinManagementService : IPinManagementService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PinManagementService> _logger;
    private readonly INotificationService _notificationService;
    private readonly Dictionary<string, (string Code, DateTime Expiry)> _verificationCodes = new();

    public PinManagementService(
        IUnitOfWork unitOfWork,
        ILogger<PinManagementService> logger,
        INotificationService notificationService)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _notificationService = notificationService;
    }

    public async Task<PinOperationResponse> SetPinAsync(SetPinRequest request, string userId)
    {
        try
        {
            var card = await GetUserCardAsync(request.CardId, userId);
            if (card == null)
            {
                return new PinOperationResponse
                { Success = false,
                    Message = "Card not found or access denied"
                };
            }

            if (card.IsBlocked())
            {
                return new PinOperationResponse
                { Success = false,
                    Message = "Cannot set PIN for blocked card",
                    IsCardBlocked = true
                };
            }

            if (!string.IsNullOrEmpty(card.PinHash))
            {
                return new PinOperationResponse
                { Success = false,
                    Message = "PIN already set. Use change PIN instead."
                };
            }

            card.PinHash = HashPin(request.NewPin);
            card.PinSetDate = DateTime.UtcNow;
            card.FailedPinAttempts = 0;

            _unitOfWork.Repository<Card>().Update(card);
            await _unitOfWork.SaveChangesAsync();

            // Send notification
            await _notificationService.SendCardAlertAsync(
                userId, 
                request.CardId, 
                "PIN Set", 
                "Your card PIN has been successfully set.");

            _logger.LogInformation("PIN set successfully for card {CardId} by user {UserId}", 
                SecureLoggingService.SanitizeInput(request.CardId), SecureLoggingService.SanitizeInput(userId));

            return new PinOperationResponse
            { Success = true,
                Message = "PIN set successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting PIN for card {CardId} by user {UserId}", 
                SecureLoggingService.SanitizeInput(request.CardId), SecureLoggingService.SanitizeInput(userId));
            
            return new PinOperationResponse
            { Success = false,
                Message = "An error occurred while setting PIN"
            };
        }
    }

    public async Task<PinOperationResponse> ChangePinAsync(SetPinRequest request, string userId)
    {
        try
        {
            var card = await GetUserCardAsync(request.CardId, userId);
            if (card == null)
            {
                return new PinOperationResponse
                { Success = false,
                    Message = "Card not found or access denied"
                };
            }

            if (card.IsBlocked())
            {
                return new PinOperationResponse
                { Success = false,
                    Message = "Cannot change PIN for blocked card",
                    IsCardBlocked = true
                };
            }

            if (string.IsNullOrEmpty(card.PinHash))
            {
                return new PinOperationResponse
                { Success = false,
                    Message = "No PIN set. Use set PIN instead."
                };
            }

            if (string.IsNullOrEmpty(request.CurrentPin))
            {
                return new PinOperationResponse
                { Success = false,
                    Message = "Current PIN is required for PIN change"
                };
            }

            // Verify current PIN
            if (!VerifyPinHash(request.CurrentPin, card.PinHash))
            {
                card.IncrementFailedPinAttempts();
                _unitOfWork.Repository<Card>().Update(card);
                await _unitOfWork.SaveChangesAsync();

                return new PinOperationResponse
                { Success = false,
                    Message = "Current PIN is incorrect",
                    IsCardBlocked = card.IsBlocked(),
                    RemainingAttempts = Math.Max(0, 3 - card.FailedPinAttempts)
                };
            }

            // Set new PIN
            card.PinHash = HashPin(request.NewPin);
            card.PinSetDate = DateTime.UtcNow;
            card.FailedPinAttempts = 0;

            _unitOfWork.Repository<Card>().Update(card);
            await _unitOfWork.SaveChangesAsync();

            // Send notification
            await _notificationService.SendCardAlertAsync(
                userId, 
                request.CardId, 
                "PIN Changed", 
                "Your card PIN has been successfully changed.");

            _logger.LogInformation("PIN changed successfully for card {CardId} by user {UserId}", 
                SecureLoggingService.SanitizeInput(request.CardId), SecureLoggingService.SanitizeInput(userId));

            return new PinOperationResponse
            { Success = true,
                Message = "PIN changed successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing PIN for card {CardId} by user {UserId}", 
                SecureLoggingService.SanitizeInput(request.CardId), SecureLoggingService.SanitizeInput(userId));
            
            return new PinOperationResponse
            { Success = false,
                Message = "An error occurred while changing PIN"
            };
        }
    }

    public async Task<PinOperationResponse> ResetPinAsync(ResetPinRequest request, string userId)
    {
        try
        {
            var card = await GetUserCardAsync(request.CardId, userId);
            if (card == null)
            {
                return new PinOperationResponse
                { Success = false,
                    Message = "Card not found or access denied"
                };
            }

            // Validate verification code
            if (!await ValidateVerificationCodeAsync(request.CardId, request.VerificationCode, userId))
            {
                return new PinOperationResponse
                { Success = false,
                    Message = "Invalid or expired verification code"
                };
            }

            // Reset PIN
            card.PinHash = HashPin(request.NewPin);
            card.PinSetDate = DateTime.UtcNow;
            card.FailedPinAttempts = 0;

            // Unblock card if it was blocked due to PIN attempts
            if (card.Status == CardStatus.Blocked && 
                card.LastBlockReason == CardBlockReason.ExcessiveDeclines)
            {
                card.Unblock();
            }

            _unitOfWork.Repository<Card>().Update(card);
            await _unitOfWork.SaveChangesAsync();

            // Remove verification code
            _verificationCodes.Remove($"{request.CardId}_{userId}");

            // Send notification
            await _notificationService.SendCardAlertAsync(
                userId, 
                request.CardId, 
                "PIN Reset", 
                "Your card PIN has been successfully reset.");

            _logger.LogInformation("PIN reset successfully for card {CardId} by user {UserId}", 
                SecureLoggingService.SanitizeInput(request.CardId), SecureLoggingService.SanitizeInput(userId));

            return new PinOperationResponse
            { Success = true,
                Message = "PIN reset successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting PIN for card {CardId} by user {UserId}", 
                SecureLoggingService.SanitizeInput(request.CardId), SecureLoggingService.SanitizeInput(userId));
            
            return new PinOperationResponse
            { Success = false,
                Message = "An error occurred while resetting PIN"
            };
        }
    }

    public async Task<PinVerificationResult> VerifyPinAsync(VerifyPinRequest request, string userId)
    {
        try
        {
            var card = await GetUserCardAsync(request.CardId, userId);
            if (card == null)
            {
                return new PinVerificationResult
                {
                    IsValid = false,
                    Message = "Card not found or access denied"
                };
            }

            if (card.IsBlocked())
            {
                return new PinVerificationResult
                {
                    IsValid = false,
                    IsCardBlocked = true,
                    Message = "Card is blocked"
                };
            }

            if (string.IsNullOrEmpty(card.PinHash))
            {
                return new PinVerificationResult
                {
                    IsValid = false,
                    Message = "No PIN set for this card"
                };
            }

            bool isValid = VerifyPinHash(request.Pin, card.PinHash);

            if (isValid)
            {
                card.ResetFailedPinAttempts();
                _unitOfWork.Repository<Card>().Update(card);
                await _unitOfWork.SaveChangesAsync();

                return new PinVerificationResult
                {
                    IsValid = true,
                    Message = "PIN verified successfully"
                };
            }
            else
            {
                card.IncrementFailedPinAttempts();
                _unitOfWork.Repository<Card>().Update(card);
                await _unitOfWork.SaveChangesAsync();

                return new PinVerificationResult
                {
                    IsValid = false,
                    IsCardBlocked = card.IsBlocked(),
                    RemainingAttempts = Math.Max(0, 3 - card.FailedPinAttempts),
                    Message = card.IsBlocked() ? "Card blocked due to excessive failed attempts" : "Invalid PIN"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying PIN for card {CardId} by user {UserId}", 
                SecureLoggingService.SanitizeInput(request.CardId), SecureLoggingService.SanitizeInput(userId));
            
            return new PinVerificationResult
            {
                IsValid = false,
                Message = "An error occurred while verifying PIN"
            };
        }
    }

    public async Task<string> GenerateVerificationCodeAsync(string cardId, string verificationMethod, string userId)
    {
        try
        {
            var card = await GetUserCardAsync(cardId, userId);
            if (card == null)
            {
                throw new InvalidOperationException("Card not found or access denied");
            }

            // Generate 6-digit verification code
            var code = GenerateRandomCode(6);
            var expiry = DateTime.UtcNow.AddMinutes(10); // 10 minutes expiry

            var key = $"{cardId}_{userId}";
            _verificationCodes[key] = (code, expiry);

            // Send verification code based on method
            if (verificationMethod.Equals("SMS", StringComparison.OrdinalIgnoreCase))
            {
                await _notificationService.SendNotificationAsync(new SendNotificationRequest
                {
                    UserId = userId,
                    Type = NotificationType.SecurityAlert,
                    Subject = "PIN Reset Verification Code",
                    Message = $"Your PIN reset verification code is: {code}. This code expires in 10 minutes.",
                    Channel = NotificationChannel.SMS,
                    Priority = NotificationPriority.High
                });
            }
            else if (verificationMethod.Equals("Email", StringComparison.OrdinalIgnoreCase))
            {
                await _notificationService.SendNotificationAsync(new SendNotificationRequest
                {
                    UserId = userId,
                    Type = NotificationType.SecurityAlert,
                    Subject = "PIN Reset Verification Code",
                    Message = $"Your PIN reset verification code is: {code}. This code expires in 10 minutes.",
                    Channel = NotificationChannel.Email,
                    Priority = NotificationPriority.High
                });
            }

            _logger.LogInformation("Verification code generated for card {CardId} PIN reset by user {UserId}", 
                cardId, userId);

            return code;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating verification code for card {CardId} by user {UserId}", 
                cardId, userId);
            throw;
        }
    }

    public async Task<bool> ValidateVerificationCodeAsync(string cardId, string verificationCode, string userId)
    {
        await Task.CompletedTask; // Make method async

        var key = $"{cardId}_{userId}";
        
        if (!_verificationCodes.TryGetValue(key, out var codeData))
        {
            return false;
        }

        if (DateTime.UtcNow > codeData.Expiry)
        {
            _verificationCodes.Remove(key);
            return false;
        }

        return codeData.Code == verificationCode;
    }

    public async Task<PinOperationResponse> UnblockCardAsync(string cardId, string userId)
    {
        try
        {
            var card = await GetUserCardAsync(cardId, userId);
            if (card == null)
            {
                return new PinOperationResponse
                { Success = false,
                    Message = "Card not found or access denied"
                };
            }

            if (card.Status != CardStatus.Blocked)
            {
                return new PinOperationResponse
                { Success = false,
                    Message = "Card is not blocked"
                };
            }

            card.Unblock();
            _unitOfWork.Repository<Card>().Update(card);
            await _unitOfWork.SaveChangesAsync();

            // Send notification
            await _notificationService.SendCardAlertAsync(
                userId, 
                cardId, 
                "Card Unblocked", 
                "Your card has been successfully unblocked.");

            _logger.LogInformation("Card {CardId} unblocked by user {UserId}", cardId, userId);

            return new PinOperationResponse
            { Success = true,
                Message = "Card unblocked successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unblocking card {CardId} by user {UserId}", cardId, userId);
            
            return new PinOperationResponse
            { Success = false,
                Message = "An error occurred while unblocking card"
            };
        }
    }

    public async Task<bool> HasPinSetAsync(string cardId, string userId)
    {
        var card = await GetUserCardAsync(cardId, userId);
        return card != null && !string.IsNullOrEmpty(card.PinHash);
    }

    private async Task<Card?> GetUserCardAsync(string cardId, string userId)
    {
        if (!Guid.TryParse(cardId, out var cardGuid) || !Guid.TryParse(userId, out var userGuid))
        {
            return null;
        }

        return await _unitOfWork.Repository<Card>().Query()
            .FirstOrDefaultAsync(c => c.Id == cardGuid && c.CustomerId == userGuid);
    }

    private static string HashPin(string pin)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(pin + "PIN_SALT"));
        return Convert.ToBase64String(hashedBytes);
    }

    private static bool VerifyPinHash(string pin, string hash)
    {
        var pinHash = HashPin(pin);
        return pinHash == hash;
    }

    private static string GenerateRandomCode(int length)
    {
        return Bank.Application.Helpers.Auth.AuthGeneratorHelper.GenerateNumericToken(length);
    }
}