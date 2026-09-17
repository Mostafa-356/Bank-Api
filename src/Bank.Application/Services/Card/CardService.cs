using Bank.Application.DTOs;
using Bank.Application.Interfaces;
using Bank.Application.Helpers;
using Bank.Application.Helpers.Shared;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bank.Application.Services;

/// <summary>
/// Service for managing card operations including issuance, activation, and transaction management
/// </summary>
public class CardService : ICardService
{
    private readonly ICardRepository _cardRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUserRepository _userRepository;
    private readonly ICardTransactionRepository _cardTransactionRepository;
    private readonly ILogger<CardService> _logger;

    public CardService(
        ICardRepository cardRepository,
        IUnitOfWork unitOfWork,
        IUserRepository userRepository,
        ICardTransactionRepository cardTransactionRepository,
        ILogger<CardService> logger)
    {
        _cardRepository = cardRepository;
        _unitOfWork = unitOfWork;
        _userRepository = userRepository;
        _cardTransactionRepository = cardTransactionRepository;
        _logger = logger;
    }

    public async Task<CardIssuanceResult> IssueCardAsync(CardIssuanceRequest request)
    {
        try
        {
            _logger.LogInformation("Starting card issuance for customer {CustomerId}, account {AccountId}", 
                request.CustomerId, request.AccountId);

            // Validate customer exists
            var customer = await _userRepository.GetByIdAsync(request.CustomerId);
            if (customer == null)
            {
                return new CardIssuanceResult
                { Success = false,
                    Message = "Customer not found",
                    Errors = new List<string> { "Invalid customer ID" }
                };
            }

            // Validate account exists and belongs to customer
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(request.AccountId);
            if (account == null || account.UserId != request.CustomerId)
            {
                return new CardIssuanceResult
                { Success = false,
                    Message = "Account not found or does not belong to customer",
                    Errors = new List<string> { "Invalid account ID" }
                };
            }

            // Check if account is active
            if (account.Status != AccountStatus.Active)
            {
                return new CardIssuanceResult
                { Success = false,
                    Message = "Cannot issue card for inactive account",
                    Errors = new List<string> { "Account is not active" }
                };
            }
            // Generate card number and security code
            var cardNumber = await GenerateCardNumberAsync(request.CardType);
            var securityCode = await GenerateSecurityCodeAsync();
            var activationCode = GenerateActivationCode();

            // Create card entity
            var card = new Card
            {
                CardNumber = cardNumber,
                MaskedCardNumber = TokenHelper.GenerateMaskedCardNumber(cardNumber),
                CustomerId = request.CustomerId,
                AccountId = request.AccountId,
                Type = request.CardType,
                Status = CardStatus.Inactive,
                ExpiryDate = DateTime.UtcNow.AddYears(3), // 3-year validity
                IssueDate = DateTime.UtcNow,
                SecurityCode = HashSecurityCode(securityCode),
                DailyLimit = request.DailyLimit ?? GetDefaultDailyLimit(request.CardType),
                MonthlyLimit = request.MonthlyLimit ?? GetDefaultMonthlyLimit(request.CardType),
                AtmDailyLimit = request.AtmDailyLimit ?? GetDefaultAtmDailyLimit(request.CardType),
                ContactlessEnabled = request.ContactlessEnabled,
                OnlineTransactionsEnabled = request.OnlineTransactionsEnabled,
                InternationalTransactionsEnabled = request.InternationalTransactionsEnabled,
                CardName = request.CardName,
                BlockedMerchantCategories = request.BlockedMerchantCategories?.Any() == true 
                    ? JsonSerializer.Serialize(request.BlockedMerchantCategories) 
                    : null
            };

            // Save card
            await _cardRepository.AddAsync(card);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Card issued successfully with ID {CardId} for customer {CustomerId}", 
                card.Id, request.CustomerId);

            return new CardIssuanceResult
            { Success = true,
                Message = "Card issued successfully",
                CardId = card.Id,
                MaskedCardNumber = card.MaskedCardNumber,
                ExpiryDate = card.ExpiryDate,
                ActivationCode = activationCode // In real implementation, this would be sent via secure channel
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error issuing card for customer {CustomerId}", request.CustomerId);
            return new CardIssuanceResult
            { Success = false,
                Message = "An error occurred while issuing the card",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<CardActivationResult> ActivateCardAsync(CardActivationRequest request)
    {
        try
        {
            _logger.LogInformation("Activating card {CardId} for customer {CustomerId}", 
                request.CardId, request.CustomerId);

            var card = await _cardRepository.GetByIdAsync(request.CardId);
            if (card == null || card.CustomerId != request.CustomerId)
            {
                return new CardActivationResult
                { Success = false,
                    Message = "Card not found",
                    Errors = new List<string> { "Invalid card ID" }
                };
            }

            if (card.Status != CardStatus.Inactive)
            {
                return new CardActivationResult
                { Success = false,
                    Message = "Card is not in inactive status",
                    Errors = new List<string> { "Card cannot be activated" }
                };
            }

            // In real implementation, validate activation code
            // For now, we'll assume it's valid if provided
            if (string.IsNullOrEmpty(request.ActivationCode))
            {
                return new CardActivationResult
                { Success = false,
                    Message = "Activation code is required",
                    Errors = new List<string> { "Invalid activation code" }
                };
            }

            // Activate card
            card.Activate(request.Channel);

            // Set PIN if provided
            if (!string.IsNullOrEmpty(request.Pin))
            {
                card.PinHash = HashPin(request.Pin);
                card.PinSetDate = DateTime.UtcNow;
            }

            _cardRepository.Update(card);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Card {CardId} activated successfully", request.CardId);

            return new CardActivationResult
            { Success = true,
                Message = "Card activated successfully",
                ActivationDate = card.ActivationDate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error activating card {CardId}", request.CardId);
            return new CardActivationResult
            { Success = false,
                Message = "An error occurred while activating the card",
                Errors = new List<string> { ex.Message }
            };
        }
    }
    public async Task<CardBlockResult> BlockCardAsync(CardBlockRequest request)
    {
        try
        {
            var card = await _cardRepository.GetByIdAsync(request.CardId);
            if (card == null || card.CustomerId != request.CustomerId)
            {
                return new CardBlockResult
                { Success = false,
                    Message = "Card not found",
                    Errors = new List<string> { "Invalid card ID" }
                };
            }

            card.Block(request.Reason);
            _cardRepository.Update(card);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Card {CardId} blocked with reason {Reason}", request.CardId, request.Reason);

            return new CardBlockResult
            { Success = true,
                Message = "Card blocked successfully",
                NewStatus = card.Status,
                StatusChangeDate = card.LastBlockedDate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error blocking card {CardId}", request.CardId);
            return new CardBlockResult
            { Success = false,
                Message = "An error occurred while blocking the card",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<CardBlockResult> UnblockCardAsync(CardUnblockRequest request)
    {
        try
        {
            var card = await _cardRepository.GetByIdAsync(request.CardId);
            if (card == null || card.CustomerId != request.CustomerId)
            {
                return new CardBlockResult
                { Success = false,
                    Message = "Card not found",
                    Errors = new List<string> { "Invalid card ID" }
                };
            }

            if (card.Status != CardStatus.Blocked)
            {
                return new CardBlockResult
                { Success = false,
                    Message = "Card is not blocked",
                    Errors = new List<string> { "Card cannot be unblocked" }
                };
            }

            card.Unblock();
            _cardRepository.Update(card);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Card {CardId} unblocked successfully", request.CardId);

            return new CardBlockResult
            { Success = true,
                Message = "Card unblocked successfully",
                NewStatus = card.Status,
                StatusChangeDate = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unblocking card {CardId}", request.CardId);
            return new CardBlockResult
            { Success = false,
                Message = "An error occurred while unblocking the card",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<CardLimitUpdateResult> UpdateLimitsAsync(CardLimitUpdateRequest request)
    {
        try
        {
            var card = await _cardRepository.GetByIdAsync(request.CardId);
            if (card == null || card.CustomerId != request.CustomerId)
            {
                return new CardLimitUpdateResult
                { Success = false,
                    Message = "Card not found",
                    Errors = new List<string> { "Invalid card ID" }
                };
            }

            if (request.DailyLimit.HasValue)
                card.DailyLimit = request.DailyLimit.Value;
            
            if (request.MonthlyLimit.HasValue)
                card.MonthlyLimit = request.MonthlyLimit.Value;
            
            if (request.AtmDailyLimit.HasValue)
                card.AtmDailyLimit = request.AtmDailyLimit.Value;

            _cardRepository.Update(card);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Card {CardId} limits updated successfully", request.CardId);

            return new CardLimitUpdateResult
            { Success = true,
                Message = "Card limits updated successfully",
                NewDailyLimit = card.DailyLimit,
                NewMonthlyLimit = card.MonthlyLimit,
                NewAtmDailyLimit = card.AtmDailyLimit
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating limits for card {CardId}", request.CardId);
            return new CardLimitUpdateResult
            { Success = false,
                Message = "An error occurred while updating card limits",
                Errors = new List<string> { ex.Message }
            };
        }
    }
    public async Task<CardDetailsDto?> GetCardDetailsAsync(Guid cardId, Guid customerId)
    {
        try
        {
            var card = await _cardRepository.GetCardWithAccountAsync(cardId);
            if (card == null || card.CustomerId != customerId)
            {
                return null;
            }

            var blockedCategories = new List<MerchantCategory>();
            if (!string.IsNullOrEmpty(card.BlockedMerchantCategories))
            {
                try
                {
                    blockedCategories = JsonSerializer.Deserialize<List<MerchantCategory>>(card.BlockedMerchantCategories) ?? new();
                }
                catch
                {
                    // Ignore deserialization errors
                }
            }

            return new CardDetailsDto
            {
                Id = card.Id,
                MaskedCardNumber = card.MaskedCardNumber,
                Type = card.Type,
                Status = card.Status,
                ExpiryDate = card.ExpiryDate,
                IssueDate = card.IssueDate,
                ActivationDate = card.ActivationDate,
                ActivationChannel = card.ActivationChannel,
                DailyLimit = card.DailyLimit,
                MonthlyLimit = card.MonthlyLimit,
                AtmDailyLimit = card.AtmDailyLimit,
                ContactlessEnabled = card.ContactlessEnabled,
                OnlineTransactionsEnabled = card.OnlineTransactionsEnabled,
                InternationalTransactionsEnabled = card.InternationalTransactionsEnabled,
                CardName = card.CardName,
                BlockedMerchantCategories = blockedCategories,
                LastBlockedDate = card.LastBlockedDate,
                LastBlockReason = card.LastBlockReason,
                HasPin = !string.IsNullOrEmpty(card.PinHash),
                PinSetDate = card.PinSetDate,
                Account = new AccountSummaryDto
                {
                    Id = card.Account.Id,
                    AccountNumber = card.Account.AccountNumber,
                    AccountHolderName = card.Account.AccountHolderName,
                    Type = card.Account.Type,
                    Balance = card.Account.Balance
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting card details for card {CardId}", cardId);
            return null;
        }
    }

    public async Task<List<CardSummaryDto>> GetCustomerCardsAsync(Guid customerId)
    {
        try
        {
            var cards = await _cardRepository.GetCardsByCustomerIdAsync(customerId);
            
            return cards.Select(card => new CardSummaryDto
            {
                Id = card.Id,
                MaskedCardNumber = card.MaskedCardNumber,
                Type = card.Type,
                Status = card.Status,
                ExpiryDate = card.ExpiryDate,
                CardName = card.CardName,
                IsActive = card.IsActive(),
                IsExpired = card.IsExpired(),
                AccountNumber = card.Account?.AccountNumber ?? ""
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cards for customer {CustomerId}", customerId);
            return new List<CardSummaryDto>();
        }
    }

    public async Task<Bank.Domain.Common.PagedResult<CardTransactionDto>> GetCardTransactionsAsync(CardTransactionSearchRequest request)
    {
        try
        {
            var card = await _cardRepository.GetByIdAsync(request.CardId);
            if (card == null || card.CustomerId != request.CustomerId)
            {
                return new PagedResult<CardTransactionDto>();
            }

            var (transactionList, totalCount) = await _cardTransactionRepository.SearchTransactionsAsync(
                request.CardId,
                request.FromDate,
                request.ToDate,
                request.TransactionType,
                request.Status,
                request.MinAmount,
                request.MaxAmount,
                request.MerchantName,
                request.Page,
                request.PageSize);

            var transactionDtos = transactionList.Select(t => new CardTransactionDto
            {
                Id = t.Id,
                CardId = t.CardId,
                NetworkTransactionId = t.Reference ?? string.Empty,
                AuthorizationCode = t.AuthorizationCode,
                Amount = t.Amount,
                CurrencyCode = t.Currency,
                Currency = t.Currency,
                TransactionType = t.TransactionType,
                Type = t.TransactionType,
                Status = t.Status,
                TransactionDate = t.TransactionDate,
                SettlementDate = t.SettlementDate,
                MerchantName = t.MerchantName,
                MerchantCategory = t.MerchantCategory,
                MerchantCity = t.MerchantCountry, // Using country as city for now
                MerchantCountryCode = t.MerchantCountry,
                IsContactless = t.IsContactless,
                IsOnline = t.IsOnline,
                IsInternational = t.IsInternational,
                Description = t.Description,
                Reference = t.Reference,
                Fee = t.Fees ?? 0,
                BalanceAfterTransaction = null // This would need to be calculated
            }).ToList();

            return new PagedResult<CardTransactionDto>
            {
                Items = transactionDtos,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transactions for card {CardId}", request.CardId);
            return new PagedResult<CardTransactionDto>();
        }
    }
    public async Task<CardPinChangeResult> ChangePinAsync(CardPinChangeRequest request)
    {
        try
        {
            var card = await _cardRepository.GetByIdAsync(request.CardId);
            if (card == null || card.CustomerId != request.CustomerId)
            {
                return new CardPinChangeResult
                { Success = false,
                    Message = "Card not found",
                    Errors = new List<string> { "Invalid card ID" }
                };
            }

            // Verify current PIN
            if (string.IsNullOrEmpty(card.PinHash) || !VerifyPin(request.CurrentPin, card.PinHash))
            {
                return new CardPinChangeResult
                { Success = false,
                    Message = "Current PIN is incorrect",
                    Errors = new List<string> { "Invalid current PIN" }
                };
            }

            // Update PIN
            card.PinHash = HashPin(request.NewPin);
            card.PinSetDate = DateTime.UtcNow;
            card.ResetFailedPinAttempts();

            _cardRepository.Update(card);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("PIN changed successfully for card {CardId}", request.CardId);

            return new CardPinChangeResult
            { Success = true,
                Message = "PIN changed successfully",
                PinChangeDate = card.PinSetDate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing PIN for card {CardId}", request.CardId);
            return new CardPinChangeResult
            { Success = false,
                Message = "An error occurred while changing PIN",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<CardPinResetResult> ResetPinAsync(CardPinResetRequest request)
    {
        try
        {
            var card = await _cardRepository.GetByIdAsync(request.CardId);
            if (card == null || card.CustomerId != request.CustomerId)
            {
                return new CardPinResetResult
                { Success = false,
                    Message = "Card not found",
                    Errors = new List<string> { "Invalid card ID" }
                };
            }

            // In real implementation, verify the verification code
            // For now, assume it's valid if provided
            if (string.IsNullOrEmpty(request.VerificationCode))
            {
                return new CardPinResetResult
                { Success = false,
                    Message = "Verification code is required",
                    Errors = new List<string> { "Invalid verification code" }
                };
            }

            // Generate new PIN
            var newPin = GenerateRandomPin();
            card.PinHash = HashPin(newPin);
            card.PinSetDate = DateTime.UtcNow;
            card.ResetFailedPinAttempts();

            _cardRepository.Update(card);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("PIN reset successfully for card {CardId}", request.CardId);

            return new CardPinResetResult
            { Success = true,
                Message = "PIN reset successfully",
                NewPin = newPin, // In real implementation, this would be sent via secure channel
                PinResetDate = card.PinSetDate
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting PIN for card {CardId}", request.CardId);
            return new CardPinResetResult
            { Success = false,
                Message = "An error occurred while resetting PIN",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    // Additional methods for merchant restrictions, contactless, online, and international settings
    public async Task<CardMerchantRestrictionsResult> UpdateMerchantRestrictionsAsync(CardMerchantRestrictionsRequest request)
    {
        try
        {
            var card = await _cardRepository.GetByIdAsync(request.CardId);
            if (card == null || card.CustomerId != request.CustomerId)
            {
                return new CardMerchantRestrictionsResult
                { Success = false,
                    Message = "Card not found",
                    Errors = new List<string> { "Invalid card ID" }
                };
            }

            card.BlockedMerchantCategories = request.BlockedCategories.Any() 
                ? JsonSerializer.Serialize(request.BlockedCategories) 
                : null;

            _cardRepository.Update(card);
            await _unitOfWork.SaveChangesAsync();

            return new CardMerchantRestrictionsResult
            { Success = true,
                Message = "Merchant restrictions updated successfully",
                BlockedCategories = request.BlockedCategories
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating merchant restrictions for card {CardId}", request.CardId);
            return new CardMerchantRestrictionsResult
            { Success = false,
                Message = "An error occurred while updating merchant restrictions",
                Errors = new List<string> { ex.Message }
            };
        }
    }
    public async Task<CardContactlessResult> UpdateContactlessSettingsAsync(CardContactlessRequest request)
    {
        try
        {
            var card = await _cardRepository.GetByIdAsync(request.CardId);
            if (card == null || card.CustomerId != request.CustomerId)
            {
                return new CardContactlessResult
                { Success = false,
                    Message = "Card not found",
                    Errors = new List<string> { "Invalid card ID" }
                };
            }

            card.ContactlessEnabled = request.Enabled;
            _cardRepository.Update(card);
            await _unitOfWork.SaveChangesAsync();

            return new CardContactlessResult
            { Success = true,
                Message = "Contactless settings updated successfully",
                ContactlessEnabled = card.ContactlessEnabled
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating contactless settings for card {CardId}", request.CardId);
            return new CardContactlessResult
            { Success = false,
                Message = "An error occurred while updating contactless settings",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<CardOnlineTransactionsResult> UpdateOnlineTransactionsAsync(CardOnlineTransactionsRequest request)
    {
        try
        {
            var card = await _cardRepository.GetByIdAsync(request.CardId);
            if (card == null || card.CustomerId != request.CustomerId)
            {
                return new CardOnlineTransactionsResult
                { Success = false,
                    Message = "Card not found",
                    Errors = new List<string> { "Invalid card ID" }
                };
            }

            card.OnlineTransactionsEnabled = request.Enabled;
            _cardRepository.Update(card);
            await _unitOfWork.SaveChangesAsync();

            return new CardOnlineTransactionsResult
            { Success = true,
                Message = "Online transactions settings updated successfully",
                OnlineTransactionsEnabled = card.OnlineTransactionsEnabled
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating online transactions settings for card {CardId}", request.CardId);
            return new CardOnlineTransactionsResult
            { Success = false,
                Message = "An error occurred while updating online transactions settings",
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<CardInternationalTransactionsResult> UpdateInternationalTransactionsAsync(CardInternationalTransactionsRequest request)
    {
        try
        {
            var card = await _cardRepository.GetByIdAsync(request.CardId);
            if (card == null || card.CustomerId != request.CustomerId)
            {
                return new CardInternationalTransactionsResult
                { Success = false,
                    Message = "Card not found",
                    Errors = new List<string> { "Invalid card ID" }
                };
            }

            card.InternationalTransactionsEnabled = request.Enabled;
            _cardRepository.Update(card);
            await _unitOfWork.SaveChangesAsync();

            return new CardInternationalTransactionsResult
            { Success = true,
                Message = "International transactions settings updated successfully",
                InternationalTransactionsEnabled = card.InternationalTransactionsEnabled
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating international transactions settings for card {CardId}", request.CardId);
            return new CardInternationalTransactionsResult
            { Success = false,
                Message = "An error occurred while updating international transactions settings",
                Errors = new List<string> { ex.Message }
            };
        }
    }
    public async Task<CardUsageStatsDto> GetCardUsageStatsAsync(Guid cardId, Guid customerId, DateTime fromDate, DateTime toDate)
    {
        try
        {
            var card = await _cardRepository.GetByIdAsync(cardId);
            if (card == null || card.CustomerId != customerId)
            {
                return new CardUsageStatsDto { CardId = cardId, FromDate = fromDate, ToDate = toDate };
            }

            var transactions = await _cardTransactionRepository.GetTransactionsByDateRangeAsync(cardId, fromDate, toDate);

            var stats = new CardUsageStatsDto
            {
                CardId = cardId,
                FromDate = fromDate,
                ToDate = toDate,
                TotalTransactions = transactions.Count,
                TotalAmount = transactions.Where(t => t.IsSuccessful()).Sum(t => t.Amount),
                TotalFees = transactions.Sum(t => t.Fees ?? 0),
                PurchaseCount = transactions.Count(t => t.TransactionType == CardTransactionType.Purchase && t.IsSuccessful()),
                PurchaseAmount = transactions.Where(t => t.TransactionType == CardTransactionType.Purchase && t.IsSuccessful()).Sum(t => t.Amount),
                WithdrawalCount = transactions.Count(t => t.TransactionType == CardTransactionType.Withdrawal && t.IsSuccessful()),
                WithdrawalAmount = transactions.Where(t => t.TransactionType == CardTransactionType.Withdrawal && t.IsSuccessful()).Sum(t => t.Amount),
                OnlineTransactionCount = transactions.Count(t => t.IsOnline && t.IsSuccessful()),
                OnlineTransactionAmount = transactions.Where(t => t.IsOnline && t.IsSuccessful()).Sum(t => t.Amount),
                InternationalTransactionCount = transactions.Count(t => t.IsInternational && t.IsSuccessful()),
                InternationalTransactionAmount = transactions.Where(t => t.IsInternational && t.IsSuccessful()).Sum(t => t.Amount)
            };

            // Calculate limit utilization
            var dailySpent = transactions.Where(t => t.TransactionDate.Date == DateTime.UtcNow.Date && t.IsSuccessful()).Sum(t => t.Amount);
            var monthlySpent = transactions.Where(t => t.TransactionDate.Year == DateTime.UtcNow.Year && 
                                                      t.TransactionDate.Month == DateTime.UtcNow.Month && 
                                                      t.IsSuccessful()).Sum(t => t.Amount);

            stats.DailyLimitUtilization = card.DailyLimit > 0 ? (dailySpent / card.DailyLimit) * 100 : 0;
            stats.MonthlyLimitUtilization = card.MonthlyLimit > 0 ? (monthlySpent / card.MonthlyLimit) * 100 : 0;

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting usage stats for card {CardId}", cardId);
            return new CardUsageStatsDto { CardId = cardId, FromDate = fromDate, ToDate = toDate };
        }
    }

    public async Task<CardValidationResult> ValidateCardForTransactionAsync(CardValidationRequest request)
    {
        try
        {
            var card = await _cardRepository.GetByIdAsync(request.CardId);
            if (card == null)
            {
                return CreateCardNotFoundResult();
            }

            var validationErrors = new List<string>();
            
            // Collect all validation errors
            ValidateCardStatus(card, validationErrors);
            ValidateCardLimits(card, request, validationErrors);
            ValidateTransactionSettings(card, request, validationErrors);
            ValidatePin(card, request, validationErrors);

            return new CardValidationResult
            {
                IsValid = !validationErrors.Any(),
                Message = validationErrors.Any() ? "Card validation failed" : "Card is valid for transaction",
                ValidationErrors = validationErrors
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating card {CardId} for transaction", request.CardId);
            return new CardValidationResult
            {
                IsValid = false,
                Message = "An error occurred during card validation",
                ValidationErrors = new List<string> { ex.Message }
            };
        }
    }

    private CardValidationResult CreateCardNotFoundResult()
    {
        return new CardValidationResult
        {
            IsValid = false,
            Message = "Card not found",
            ValidationErrors = new List<string> { "Invalid card ID" }
        };
    }

    private void ValidateCardStatus(Domain.Entities.Card card, List<string> validationErrors)
    {
        if (!card.IsActive())
        {
            validationErrors.Add("Card is not active");
        }

        if (card.IsExpired())
        {
            validationErrors.Add("Card has expired");
        }

        if (card.IsBlocked())
        {
            validationErrors.Add("Card is blocked");
        }
    }

    private void ValidateCardLimits(Domain.Entities.Card card, CardValidationRequest request, List<string> validationErrors)
    {
        if (!card.IsWithinLimits(request.Amount, DateTime.UtcNow))
        {
            validationErrors.Add("Transaction amount exceeds card limits");
        }

        if (request.MerchantCategory.HasValue && !card.IsMerchantCategoryAllowed(request.MerchantCategory.Value))
        {
            validationErrors.Add("Merchant category is blocked");
        }
    }

    private void ValidateTransactionSettings(Domain.Entities.Card card, CardValidationRequest request, List<string> validationErrors)
    {
        if (request.IsOnline && !card.OnlineTransactionsEnabled)
        {
            validationErrors.Add("Online transactions are disabled");
        }

        if (request.IsInternational && !card.InternationalTransactionsEnabled)
        {
            validationErrors.Add("International transactions are disabled");
        }
    }

    private void ValidatePin(Domain.Entities.Card card, CardValidationRequest request, List<string> validationErrors)
    {
        if (string.IsNullOrEmpty(request.Pin) || string.IsNullOrEmpty(card.PinHash))
        {
            return;
        }

        if (!VerifyPin(request.Pin, card.PinHash))
        {
            validationErrors.Add("Invalid PIN");
        }
    }
    public async Task<string> GenerateCardNumberAsync(CardType cardType)
    {
        // Generate a 16-digit card number
        // In real implementation, this would follow proper card number generation algorithms (Luhn algorithm)
        // and ensure uniqueness across the system
        
        string prefix = cardType switch
        {
            CardType.Debit => "4000", // Visa debit
            CardType.Credit => "5000", // Mastercard credit
            CardType.Prepaid => "6000", // Prepaid
            CardType.Business => "5100", // Business
            CardType.Premium => "4100", // Premium
            _ => "4000"
        };

        string cardNumber;
        bool isUnique;
        
        do
        {
            // Generate remaining 12 digits using cryptographically secure random
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            var bytes = new byte[12];
            rng.GetBytes(bytes);
            var remainingDigits = string.Join("", bytes.Select(b => (b % 10).ToString()));
            cardNumber = prefix + remainingDigits;
            
            // Check if card number is unique
            isUnique = await _cardRepository.IsCardNumberUniqueAsync(cardNumber);
        } 
        while (!isUnique);

        return cardNumber;
    }

    public async Task<string> GenerateSecurityCodeAsync()
    {
        // Generate a 3-digit CVV using centralized helper
        await Task.CompletedTask; // Placeholder for async operation
        return Bank.Application.Helpers.Auth.AuthGeneratorHelper.GenerateNumericToken(3);
    }

    // Private helper methods
    private static string GenerateActivationCode()
    {
        // Generate 6-digit activation code using centralized helper
        return Bank.Application.Helpers.Auth.AuthGeneratorHelper.GenerateNumericToken(6);
    }

    private static string GenerateRandomPin()
    {
        // Generate 4-digit PIN using centralized helper
        return Bank.Application.Helpers.Auth.AuthGeneratorHelper.GenerateRandomPin(4);
    }

    private static string HashSecurityCode(string securityCode)
    {
        // In real implementation, use proper encryption/hashing
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(securityCode + "SECURITY_SALT"));
        return Convert.ToBase64String(hashedBytes);
    }

    private static string HashPin(string pin)
    {
        // In real implementation, use proper encryption/hashing with salt
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(pin + "PIN_SALT"));
        return Convert.ToBase64String(hashedBytes);
    }

    private static bool VerifyPin(string pin, string hashedPin)
    {
        var hashedInput = HashPin(pin);
        return hashedInput == hashedPin;
    }

    private static decimal GetDefaultDailyLimit(CardType cardType)
    {
        return cardType switch
        {
            CardType.Debit => 5000m,
            CardType.Credit => 10000m,
            CardType.Prepaid => 2000m,
            CardType.Business => 20000m,
            CardType.Premium => 50000m,
            _ => 5000m
        };
    }

    private static decimal GetDefaultMonthlyLimit(CardType cardType)
    {
        return cardType switch
        {
            CardType.Debit => 50000m,
            CardType.Credit => 100000m,
            CardType.Prepaid => 20000m,
            CardType.Business => 200000m,
            CardType.Premium => 500000m,
            _ => 50000m
        };
    }

    private static decimal GetDefaultAtmDailyLimit(CardType cardType)
    {
        return cardType switch
        {
            CardType.Debit => 2000m,
            CardType.Credit => 5000m,
            CardType.Prepaid => 1000m,
            CardType.Business => 10000m,
            CardType.Premium => 20000m,
            _ => 2000m
        };
    }
}



