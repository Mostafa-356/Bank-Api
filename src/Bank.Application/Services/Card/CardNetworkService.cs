using Bank.Application.DTOs;
using Bank.Application.Helpers.Shared;
using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;

namespace Bank.Application.Services;

/// <summary>
/// Service for card network integration including authorization, settlement, and transaction processing
/// </summary>
public class CardNetworkService : ICardNetworkService
{
    private readonly ICardRepository _cardRepository;
    private readonly ICardTransactionRepository _cardTransactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<CardNetworkService> _logger;

    public CardNetworkService(
        ICardRepository cardRepository,
        ICardTransactionRepository cardTransactionRepository,
        IUnitOfWork unitOfWork,
        IAuditLogService auditLogService,
        ILogger<CardNetworkService> logger)
    {
        _cardRepository = cardRepository;
        _cardTransactionRepository = cardTransactionRepository;
        _unitOfWork = unitOfWork;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    public async Task<CardAuthorizationResult> AuthorizeTransactionAsync(CardAuthorizationRequest request)
    {
        try
        {
            var card = await _cardRepository.GetByIdAsync(request.CardId);
            if (card == null)
            {
                return new CardAuthorizationResult
                { Success = false,
                    ResponseCode = "14",
                    ResponseMessage = "Invalid card",
                    DeclineReason = "Card not found"
                };
            }

            // Validate card status
            var validationResult = await ValidateCardForTransactionAsync(card, request.Amount);
            if (!validationResult.IsValid)
            {
                return new CardAuthorizationResult
                { Success = false,
                    ResponseCode = validationResult.ResponseCode,
                    ResponseMessage = validationResult.ResponseMessage,
                    DeclineReason = validationResult.DeclineReason
                };
            }

            // Calculate fees
            var fees = await CalculateTransactionFeesAsync(new CardTransactionFeeRequest
            {
                Amount = request.Amount,
                Network = GetCardNetwork(card.CardNumber),
                TransactionType = CardTransactionType.Purchase,
                MerchantCategory = request.MerchantCategory,
                IsInternational = request.IsInternational,
                IsOnline = request.IsOnline
            });

            // Generate authorization code
            var authCode = GenerateAuthorizationCode();
            var transactionId = Guid.NewGuid().ToString();

            // Create authorization record
            var authorization = new CardAuthorization
            {
                Id = Guid.NewGuid(),
                CardId = request.CardId,
                AuthorizationCode = authCode,
                Amount = request.Amount,
                Currency = request.Currency,
                MerchantId = request.MerchantId,
                MerchantName = request.MerchantName,
                MerchantCategory = request.MerchantCategory,
                TransactionDate = request.TransactionDate,
                ExpiresAt = DateTime.UtcNow.AddMinutes(30), // 30-minute expiry
                Status = CardTransactionStatus.Authorized,
                IsInternational = request.IsInternational,
                IsOnline = request.IsOnline,
                IsContactless = request.IsContactless
            };

            await _cardTransactionRepository.AddAuthorizationAsync(authorization);

            await _auditLogService.LogAsync(
                "CardAuthorization",
                $"Card authorization successful for card {card.MaskedCardNumber}",
                card.CustomerId);

            return new CardAuthorizationResult
            { Success = true,
                AuthorizationCode = authCode,
                TransactionId = transactionId,
                ResponseCode = "00",
                ResponseMessage = "Approved",
                AuthorizedAmount = request.Amount,
                AuthorizationDate = DateTime.UtcNow,
                Fees = fees
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error authorizing card transaction for card {CardId}", request.CardId);
            return new CardAuthorizationResult
            { Success = false,
                ResponseCode = "96",
                ResponseMessage = "System error",
                DeclineReason = "Processing error"
            };
        }
    }
    public async Task<CardCaptureResult> CaptureTransactionAsync(CardCaptureRequest request)
    {
        try
        {
            var authorization = await _cardTransactionRepository.GetAuthorizationByCodeAsync(request.AuthorizationCode);
            if (authorization == null)
            {
                return new CardCaptureResult
                { Success = false,
                    ErrorMessage = "Authorization not found"
                };
            }

            if (authorization.Status != CardTransactionStatus.Authorized)
            {
                return new CardCaptureResult
                { Success = false,
                    ErrorMessage = "Authorization not valid for capture"
                };
            }

            if (authorization.ExpiresAt < DateTime.UtcNow)
            {
                return new CardCaptureResult
                { Success = false,
                    ErrorMessage = "Authorization expired"
                };
            }

            // Update authorization status
            authorization.Status = CardTransactionStatus.Settled;
            authorization.CapturedAmount = request.CaptureAmount;
            authorization.CaptureDate = DateTime.UtcNow;

            await _cardTransactionRepository.UpdateAuthorizationAsync(authorization);

            // Create transaction record
            var transaction = new CardTransaction
            {
                Id = Guid.NewGuid(),
                CardId = authorization.CardId,
                AuthorizationCode = request.AuthorizationCode,
                Amount = request.CaptureAmount,
                TransactionType = CardTransactionType.Purchase,
                Status = CardTransactionStatus.Settled,
                MerchantId = authorization.MerchantId,
                MerchantName = authorization.MerchantName,
                MerchantCategory = authorization.MerchantCategory,
                TransactionDate = DateTime.UtcNow,
                Reference = request.Reference
            };

            await _cardTransactionRepository.AddTransactionAsync(transaction);

            return new CardCaptureResult
            { Success = true,
                CaptureId = transaction.Id.ToString(),
                CapturedAmount = request.CaptureAmount,
                CaptureDate = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing transaction with authorization code {AuthCode}", request.AuthorizationCode);
            return new CardCaptureResult
            { Success = false,
                ErrorMessage = "Capture processing error"
            };
        }
    }

    public async Task<CardVoidResult> VoidTransactionAsync(CardVoidRequest request)
    {
        try
        {
            var authorization = await _cardTransactionRepository.GetAuthorizationByCodeAsync(request.AuthorizationCode);
            if (authorization == null)
            {
                return new CardVoidResult
                { Success = false,
                    ErrorMessage = "Authorization not found"
                };
            }

            if (authorization.Status != CardTransactionStatus.Authorized)
            {
                return new CardVoidResult
                { Success = false,
                    ErrorMessage = "Authorization cannot be voided"
                };
            }

            // Update authorization status
            authorization.Status = CardTransactionStatus.Reversed;
            authorization.VoidDate = DateTime.UtcNow;
            authorization.VoidReason = request.Reason;

            await _cardTransactionRepository.UpdateAuthorizationAsync(authorization);

            return new CardVoidResult
            { Success = true,
                VoidId = Guid.NewGuid().ToString(),
                VoidDate = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error voiding transaction with authorization code {AuthCode}", request.AuthorizationCode);
            return new CardVoidResult
            { Success = false,
                ErrorMessage = "Void processing error"
            };
        }
    }

    public async Task<CardRefundResult> RefundTransactionAsync(CardRefundRequest request)
    {
        try
        {
            var transaction = await _cardTransactionRepository.GetTransactionByIdAsync(Guid.Parse(request.TransactionId));
            if (transaction == null)
            {
                return new CardRefundResult
                { Success = false,
                    ErrorMessage = "Transaction not found"
                };
            }

            if (transaction.Status != CardTransactionStatus.Settled)
            {
                return new CardRefundResult
                { Success = false,
                    ErrorMessage = "Transaction cannot be refunded"
                };
            }

            // Create refund transaction
            var refundTransaction = new CardTransaction
            {
                Id = Guid.NewGuid(),
                CardId = transaction.CardId,
                Amount = -request.RefundAmount, // Negative amount for refund
                TransactionType = CardTransactionType.Refund,
                Status = CardTransactionStatus.Settled,
                MerchantId = transaction.MerchantId,
                MerchantName = transaction.MerchantName,
                TransactionDate = DateTime.UtcNow,
                Reference = $"Refund for {request.TransactionId}",
                OriginalTransactionId = transaction.Id
            };

            await _cardTransactionRepository.AddTransactionAsync(refundTransaction);

            return new CardRefundResult
            { Success = true,
                RefundId = refundTransaction.Id.ToString(),
                RefundedAmount = request.RefundAmount,
                RefundDate = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing refund for transaction {TransactionId}", request.TransactionId);
            return new CardRefundResult
            { Success = false,
                ErrorMessage = "Refund processing error"
            };
        }
    }
    public async Task<CardSettlementResult> ProcessSettlementAsync(CardSettlementRequest request)
    {
        try
        {
            var transactions = await _cardTransactionRepository.GetTransactionsForSettlementAsync(
                request.SettlementDate, request.TransactionIds, request.Network);

            var processedTransactions = new List<string>();
            var failedTransactions = new List<string>();
            decimal totalAmount = 0;
            decimal totalFees = 0;

            foreach (var transaction in transactions)
            {
                try
                {
                    // Process settlement for each transaction
                    transaction.SettlementDate = request.SettlementDate;
                    transaction.Status = CardTransactionStatus.Settled;
                    
                    await _cardTransactionRepository.UpdateTransactionAsync(transaction);
                    
                    processedTransactions.Add(transaction.Id.ToString());
                    totalAmount += transaction.Amount;
                    
                    // Calculate fees (simplified)
                    var transactionFees = Math.Abs(transaction.Amount) * 0.025m; // 2.5% fee
                    totalFees += transactionFees;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error settling transaction {TransactionId}", transaction.Id);
                    failedTransactions.Add(transaction.Id.ToString());
                }
            }

            var settlementId = Guid.NewGuid().ToString();
            var netAmount = totalAmount - totalFees;

            return new CardSettlementResult
            { Success = failedTransactions.Count == 0,
                SettlementId = settlementId,
                SettlementDate = request.SettlementDate,
                TransactionCount = processedTransactions.Count,
                TotalAmount = totalAmount,
                TotalFees = totalFees,
                NetAmount = netAmount,
                ProcessedTransactions = processedTransactions,
                FailedTransactions = failedTransactions
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing settlement for date {SettlementDate}", request.SettlementDate);
            return new CardSettlementResult
            { Success = false,
                ErrorMessage = "Settlement processing error"
            };
        }
    }

    public async Task<CardSettlementReport> GetSettlementReportAsync(DateTime settlementDate)
    {
        try
        {
            var transactions = await _cardTransactionRepository.GetSettledTransactionsByDateAsync(settlementDate);
            
            var networkSummaries = transactions
                .GroupBy(t => GetCardNetwork(t.Card?.CardNumber ?? ""))
                .Select(g => new CardSettlementSummary
                {
                    Network = g.Key,
                    TransactionCount = g.Count(),
                    GrossAmount = g.Sum(t => Math.Abs(t.Amount)),
                    InterchangeFees = g.Sum(t => Math.Abs(t.Amount) * 0.015m), // 1.5%
                    ProcessingFees = g.Sum(t => Math.Abs(t.Amount) * 0.01m),   // 1%
                    NetAmount = g.Sum(t => Math.Abs(t.Amount) * 0.975m)        // 97.5%
                })
                .ToList();

            return new CardSettlementReport
            {
                SettlementDate = settlementDate,
                NetworkSummaries = networkSummaries,
                TotalSettlementAmount = networkSummaries.Sum(s => s.GrossAmount),
                TotalFees = networkSummaries.Sum(s => s.InterchangeFees + s.ProcessingFees),
                NetSettlementAmount = networkSummaries.Sum(s => s.NetAmount),
                TotalTransactionCount = networkSummaries.Sum(s => s.TransactionCount)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating settlement report for date {SettlementDate}", settlementDate);
            throw;
        }
    }

    public async Task<CardTransactionResult> ProcessCardTransactionAsync(CardTransactionRequest request)
    {
        try
        {
            var card = await _cardRepository.GetByIdAsync(request.CardId);
            if (card == null)
            {
                return new CardTransactionResult
                { Success = false,
                    ErrorMessage = "Card not found"
                };
            }

            // Validate card and transaction
            var validationResult = await ValidateCardForTransactionAsync(card, request.Amount);
            if (!validationResult.IsValid)
            {
                return new CardTransactionResult
                { Success = false,
                    ErrorMessage = validationResult.ResponseMessage,
                    DeclineReason = validationResult.DeclineReason
                };
            }

            // Create transaction
            var transaction = new CardTransaction
            {
                Id = Guid.NewGuid(),
                CardId = request.CardId,
                Amount = request.Amount,
                TransactionType = request.TransactionType,
                Status = CardTransactionStatus.Pending,
                MerchantId = request.MerchantId,
                MerchantName = request.MerchantName,
                MerchantCategory = request.MerchantCategory,
                Currency = request.Currency,
                Description = request.Description,
                Reference = request.Reference,
                TransactionDate = request.TransactionDate,
                IsInternational = request.IsInternational,
                IsOnline = request.IsOnline,
                IsContactless = request.IsContactless,
                AuthorizationCode = request.AuthorizationCode
            };

            await _cardTransactionRepository.AddTransactionAsync(transaction);

            // Update account balance (for debit cards)
            if (card.Type == CardType.Debit)
            {
                var account = await _unitOfWork.Repository<Account>().GetByIdAsync(card.AccountId);
                if (account != null)
                {
                    account.Balance -= request.Amount;
                    _unitOfWork.Repository<Account>().Update(account);
                }
            }

            // Calculate fees
            var fees = await CalculateTransactionFeesAsync(new CardTransactionFeeRequest
            {
                Amount = request.Amount,
                Network = GetCardNetwork(card.CardNumber),
                TransactionType = request.TransactionType,
                MerchantCategory = request.MerchantCategory,
                IsInternational = request.IsInternational,
                IsOnline = request.IsOnline
            });

            transaction.Status = CardTransactionStatus.Settled;
            await _cardTransactionRepository.UpdateTransactionAsync(transaction);

            return new CardTransactionResult
            { Success = true,
                TransactionId = transaction.Id,
                AuthorizationCode = GenerateAuthorizationCode(),
                Status = CardTransactionStatus.Settled,
                ProcessedAmount = request.Amount,
                AccountBalance = card.Type == CardType.Debit ? 
                    (await _unitOfWork.Repository<Account>().GetByIdAsync(card.AccountId))?.Balance ?? 0 : 0,
                Fees = fees
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing card transaction for card {CardId}", request.CardId);
            return new CardTransactionResult
            { Success = false,
                ErrorMessage = "Transaction processing error"
            };
        }
    }
    public async Task<CardTransactionDto> GetCardTransactionAsync(Guid transactionId)
    {
        try
        {
            var transaction = await _cardTransactionRepository.GetTransactionByIdAsync(transactionId);
            if (transaction == null)
                return null;

            return MapToCardTransactionDto(transaction);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving card transaction {TransactionId}", transactionId);
            throw;
        }
    }

    public async Task<Bank.Domain.Common.PagedResult<CardTransactionDto>> GetCardTransactionsAsync(CardTransactionSearchRequest request)
    {
        try
        {
            var (transactions, totalCount) = await _cardTransactionRepository.SearchTransactionsAsync(
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
            
            var transactionDtos = transactions.Select(MapToCardTransactionDto).ToList();
            
            return new Bank.Domain.Common.PagedResult<CardTransactionDto>
            {
                Items = transactionDtos,
                TotalCount = totalCount,
                Page = request.Page,
                PageSize = request.PageSize
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching card transactions");
            throw;
        }
    }

    public async Task<CardStatementResult> GenerateCardStatementAsync(CardStatementRequest request)
    {
        try
        {
            var card = await _cardRepository.GetByIdAsync(request.CardId);
            if (card == null)
            {
                return new CardStatementResult
                { Success = false,
                    ErrorMessage = "Card not found"
                };
            }

            var transactions = await _cardTransactionRepository.GetTransactionsByDateRangeAsync(
                request.CardId, request.FromDate, request.ToDate);

            var statement = new CardStatement
            {
                Id = Guid.NewGuid(),
                CardId = request.CardId,
                FromDate = request.FromDate,
                ToDate = request.ToDate,
                GeneratedDate = DateTime.UtcNow,
                Format = request.Format,
                TransactionCount = transactions.Count,
                TotalSpent = transactions.Where(t => t.Amount > 0).Sum(t => t.Amount),
                TotalFees = transactions.Sum(t => t.Fees ?? 0)
            };

            // Generate statement content based on format
            byte[] content = request.Format switch
            {
                StatementFormat.PDF => await GeneratePdfStatementAsync(statement, transactions),
                StatementFormat.CSV => GenerateCsvStatement(statement, transactions),
                StatementFormat.Excel => await GenerateExcelStatementAsync(statement, transactions),
                _ => throw new NotSupportedException($"Format {request.Format} not supported")
            };

            var fileName = $"card_statement_{card.MaskedCardNumber}_{request.FromDate:yyyyMM}_{request.ToDate:yyyyMM}.{request.Format.ToString().ToLower()}";
            
            // Save statement record
            await _cardTransactionRepository.AddStatementAsync(statement);

            return new CardStatementResult
            { Success = true,
                StatementId = statement.Id,
                FileName = fileName,
                Content = content,
                Format = request.Format,
                GeneratedDate = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating card statement for card {CardId}", request.CardId);
            return new CardStatementResult
            { Success = false,
                ErrorMessage = "Statement generation error"
            };
        }
    }

    public async Task<CardStatementDto> GetCardStatementAsync(Guid statementId)
    {
        try
        {
            var statement = await _cardTransactionRepository.GetStatementByIdAsync(statementId);
            if (statement == null)
                return null;

            return MapToCardStatementDto(statement);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving card statement {StatementId}", statementId);
            throw;
        }
    }

    public async Task<List<CardStatementDto>> GetCardStatementsAsync(Guid cardId, int? limit = null)
    {
        try
        {
            var statements = await _cardTransactionRepository.GetCardStatementsAsync(cardId, limit);
            return statements.Select(MapToCardStatementDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving card statements for card {CardId}", cardId);
            throw;
        }
    }
    public async Task<CardRenewalResult> ProcessCardRenewalsAsync(int daysBeforeExpiry = 60)
    {
        try
        {
            var expiringCards = await _cardRepository.GetCardsExpiringInDaysAsync(daysBeforeExpiry);
            var renewalDetails = new List<CardRenewalInfo>();
            int successfulRenewals = 0;
            int failedRenewals = 0;

            foreach (var card in expiringCards)
            {
                try
                {
                    var renewalResult = await RenewCardAsync(card.Id);
                    if (renewalResult.Success && renewalResult.RenewalDetails.Any())
                    {
                        var renewal = renewalResult.RenewalDetails.First();
                        renewalDetails.Add(renewal);
                        if (renewal.Success)
                            successfulRenewals++;
                        else
                            failedRenewals++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error renewing card {CardId}", card.Id);
                    renewalDetails.Add(new CardRenewalInfo
                    {
                        CardId = card.Id,
                        CardNumber = card.MaskedCardNumber,
                        OldExpiryDate = card.ExpiryDate, Success = false,
                        ErrorMessage = ex.Message
                    });
                    failedRenewals++;
                }
            }

            return new CardRenewalResult
            { Success = failedRenewals == 0,
                ProcessedCount = renewalDetails.Count,
                SuccessfulRenewals = successfulRenewals,
                FailedRenewals = failedRenewals,
                RenewalDetails = renewalDetails
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing card renewals");
            return new CardRenewalResult
            { Success = false,
                ErrorMessage = "Card renewal processing error"
            };
        }
    }

    public async Task<CardRenewalResult> RenewCardAsync(Guid cardId)
    {
        try
        {
            var oldCard = await _cardRepository.GetByIdAsync(cardId);
            if (oldCard == null)
            {
                return new CardRenewalResult
                { Success = false,
                    ErrorMessage = "Card not found"
                };
            }

            // Create new card with updated expiry date
            var newCard = new Card
            {
                Id = Guid.NewGuid(),
                CustomerId = oldCard.CustomerId,
                AccountId = oldCard.AccountId,
                CardNumber = GenerateNewCardNumber(),
                ExpiryDate = DateTime.UtcNow.AddYears(3), // 3-year validity
                SecurityCode = GenerateCVV(),
                Type = oldCard.Type,
                Status = CardStatus.Inactive, // Requires activation
                DailyLimit = oldCard.DailyLimit,
                MonthlyLimit = oldCard.MonthlyLimit,
                ContactlessEnabled = oldCard.ContactlessEnabled,
                OnlineTransactionsEnabled = oldCard.OnlineTransactionsEnabled,
                InternationalTransactionsEnabled = oldCard.InternationalTransactionsEnabled,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "System"
            };

            await _cardRepository.AddAsync(newCard);

            // Update old card status
            oldCard.Status = CardStatus.Expired;
            oldCard.UpdatedAt = DateTime.UtcNow;
            oldCard.UpdatedBy = "System";
            _cardRepository.Update(oldCard);

            await _auditLogService.LogAsync(
                "CardRenewal",
                $"Card renewed: {oldCard.MaskedCardNumber} -> {newCard.MaskedCardNumber}",
                oldCard.CustomerId);

            return new CardRenewalResult
            { Success = true,
                ProcessedCount = 1,
                SuccessfulRenewals = 1,
                RenewalDetails = new List<CardRenewalInfo>
                {
                    new CardRenewalInfo
                    {
                        CardId = oldCard.Id,
                        CardNumber = oldCard.MaskedCardNumber,
                        NewCardId = newCard.Id,
                        NewCardNumber = newCard.MaskedCardNumber,
                        OldExpiryDate = oldCard.ExpiryDate,
                        NewExpiryDate = newCard.ExpiryDate, Success = true
                    }
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error renewing card {CardId}", cardId);
            return new CardRenewalResult
            { Success = false,
                ErrorMessage = "Card renewal error"
            };
        }
    }

    public async Task<List<CardSummaryDto>> GetCardsApproachingExpiryAsync(int daysBeforeExpiry = 60)
    {
        try
        {
            var cards = await _cardRepository.GetCardsExpiringInDaysAsync(daysBeforeExpiry);
            return cards.Select(MapToCardSummaryDto).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cards approaching expiry");
            throw;
        }
    }

    public async Task<CardValidationResult> ValidateCardAsync(CardValidationRequest request)
    {
        try
        {
            var card = await _cardRepository.GetByIdAsync(request.CardId);
            if (card == null)
            {
                return new CardValidationResult
                {
                    IsValid = false,
                    ResponseCode = "14",
                    ResponseMessage = "Invalid card",
                    DeclineReason = "Card not found"
                };
            }

            return await ValidateCardForTransactionAsync(card, request.Amount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating card {CardId}", request.CardId);
            return new CardValidationResult
            {
                IsValid = false,
                ResponseCode = "96",
                ResponseMessage = "System error",
                DeclineReason = "Validation error"
            };
        }
    }
    public async Task<CardNetworkStatus> GetNetworkStatusAsync(CardNetwork network)
    {
        try
        {
            // Simulate network status check
            var startTime = DateTime.UtcNow;
            await Task.Delay(100); // Simulate network call
            var endTime = DateTime.UtcNow;

            return new CardNetworkStatus
            {
                Network = network,
                IsOnline = true, // In real implementation, this would be actual network check
                LastStatusCheck = DateTime.UtcNow,
                StatusMessage = "Network operational",
                ResponseTime = endTime - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking network status for {Network}", network);
            return new CardNetworkStatus
            {
                Network = network,
                IsOnline = false,
                LastStatusCheck = DateTime.UtcNow,
                StatusMessage = "Network check failed"
            };
        }
    }

    public async Task<BatchSettlementResult> ProcessBatchSettlementAsync(string settlementFilePath)
    {
        try
        {
            // In real implementation, this would parse settlement files from card networks
            // For now, we'll simulate batch processing
            
            var batchId = Guid.NewGuid().ToString();
            var processedDate = DateTime.UtcNow;
            
            // Simulate processing settlement file
            await Task.Delay(1000);
            
            return new BatchSettlementResult
            { Success = true,
                BatchId = batchId,
                ProcessedDate = processedDate,
                TotalRecords = 100, // Simulated
                ProcessedRecords = 98,
                FailedRecords = 2,
                TotalAmount = 50000.00m,
                Errors = new List<string> { "Record 45: Invalid merchant ID", "Record 78: Amount mismatch" }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing batch settlement file {FilePath}", settlementFilePath);
            return new BatchSettlementResult
            { Success = false,
                Errors = new List<string> { ex.Message }
            };
        }
    }

    public async Task<CardTransactionFees> CalculateTransactionFeesAsync(CardTransactionFeeRequest request)
    {
        try
        {
            // Fee calculation based on network, transaction type, and other factors
            decimal interchangeFee = request.Amount * GetInterchangeRate(request.Network, request.MerchantCategory, request.IsInternational);
            decimal processingFee = request.Amount * 0.01m; // 1% processing fee
            decimal networkFee = request.IsInternational ? 0.50m : 0.25m; // Fixed network fee

            return new CardTransactionFees
            {
                InterchangeFee = interchangeFee,
                ProcessingFee = processingFee,
                NetworkFee = networkFee,
                TotalFees = interchangeFee + processingFee + networkFee,
                Currency = request.Currency
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating transaction fees");
            throw;
        }
    }

    #region Private Helper Methods

    private async Task<CardValidationResult> ValidateCardForTransactionAsync(Card card, decimal amount)
    {
        // Check card status
        if (card.Status != CardStatus.Active)
        {
            return new CardValidationResult
            {
                IsValid = false,
                ResponseCode = "04",
                ResponseMessage = "Card not active",
                DeclineReason = $"Card status: {card.Status}"
            };
        }

        // Check expiry date
        if (card.ExpiryDate < DateTime.UtcNow)
        {
            return new CardValidationResult
            {
                IsValid = false,
                ResponseCode = "54",
                ResponseMessage = "Card expired",
                DeclineReason = "Card has expired"
            };
        }

        // Check daily limit
        var todayTransactions = await _cardTransactionRepository.GetTodayTransactionsAsync(card.Id);
        var todaySpent = todayTransactions.Where(t => t.Amount > 0).Sum(t => t.Amount);
        
        if (todaySpent + amount > card.DailyLimit)
        {
            return new CardValidationResult
            {
                IsValid = false,
                ResponseCode = "61",
                ResponseMessage = "Daily limit exceeded",
                DeclineReason = "Transaction would exceed daily limit"
            };
        }

        // Check account balance (for debit cards)
        if (card.Type == CardType.Debit)
        {
            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(card.AccountId);
            if (account == null || account.Balance < amount)
            {
                return new CardValidationResult
                {
                    IsValid = false,
                    ResponseCode = "51",
                    ResponseMessage = "Insufficient funds",
                    DeclineReason = "Account balance insufficient"
                };
            }
        }

        return new CardValidationResult
        {
            IsValid = true,
            ResponseCode = "00",
            ResponseMessage = "Approved"
        };
    }

    private static CardNetwork GetCardNetwork(string cardNumber)
    {
        if (string.IsNullOrEmpty(cardNumber) || cardNumber.Length < 4)
            return CardNetwork.Visa; // Default

        var firstFour = cardNumber.Substring(0, 4);
        return firstFour[0] switch
        {
            '4' => CardNetwork.Visa,
            '5' => CardNetwork.Mastercard,
            '3' => CardNetwork.AmericanExpress,
            '6' => CardNetwork.Discover,
            _ => CardNetwork.Visa
        };
    }

    private static decimal GetInterchangeRate(CardNetwork network, MerchantCategory category, bool isInternational)
    {
        decimal baseRate = network switch
        {
            CardNetwork.Visa => 0.015m,
            CardNetwork.Mastercard => 0.016m,
            CardNetwork.AmericanExpress => 0.025m,
            _ => 0.015m
        };

        // Adjust for merchant category
        if (category == MerchantCategory.Grocery)
            baseRate *= 0.8m; // Lower rate for grocery
        else if (category == MerchantCategory.Gas)
            baseRate *= 0.9m; // Lower rate for gas

        // International surcharge
        if (isInternational)
            baseRate += 0.005m;

        return baseRate;
    }

    private static string GenerateAuthorizationCode()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[4];
        rng.GetBytes(bytes);
        var value = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 900000 + 100000;
        return value.ToString();
    }

    private static string GenerateNewCardNumber()
    {
        // Generate a new 16-digit card number (simplified)
        using var rng = RandomNumberGenerator.Create();
        var cardNumberBuilder = new System.Text.StringBuilder("4"); // Visa prefix
        for (int i = 1; i < 16; i++)
        {
            var bytes = new byte[1];
            rng.GetBytes(bytes);
            cardNumberBuilder.Append((bytes[0] % 10).ToString());
        }
        return cardNumberBuilder.ToString();
    }

    private static string GenerateCVV()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[2];
        rng.GetBytes(bytes);
        var value = Math.Abs(BitConverter.ToInt16(bytes, 0)) % 900 + 100;
        return value.ToString();
    }
    private CardTransactionDto MapToCardTransactionDto(CardTransaction transaction)
    {
        return new CardTransactionDto
        {
            Id = transaction.Id,
            CardId = transaction.CardId,
            Amount = transaction.Amount,
            TransactionType = transaction.TransactionType,
            Status = transaction.Status,
            MerchantName = transaction.MerchantName,
            MerchantCategory = transaction.MerchantCategory,
            Currency = transaction.Currency,
            Description = transaction.Description,
            Reference = transaction.Reference,
            TransactionDate = transaction.TransactionDate,
            AuthorizationCode = transaction.AuthorizationCode,
            IsInternational = transaction.IsInternational,
            IsOnline = transaction.IsOnline,
            IsContactless = transaction.IsContactless
        };
    }

    private CardStatementDto MapToCardStatementDto(CardStatement statement)
    {
        return new CardStatementDto
        {
            Id = statement.Id,
            CardId = statement.CardId,
            CardNumber = statement.Card?.MaskedCardNumber ?? "",
            FromDate = statement.FromDate,
            ToDate = statement.ToDate,
            GeneratedDate = statement.GeneratedDate,
            Format = statement.Format,
            FileName = $"statement_{statement.Id}",
            TransactionCount = statement.TransactionCount,
            TotalSpent = statement.TotalSpent,
            TotalFees = statement.TotalFees,
            PreviousBalance = statement.PreviousBalance,
            CurrentBalance = statement.CurrentBalance
        };
    }

    private CardSummaryDto MapToCardSummaryDto(Card card)
    {
        return new CardSummaryDto
        {
            Id = card.Id,
            CustomerId = card.CustomerId,
            AccountId = card.AccountId,
            MaskedCardNumber = card.MaskedCardNumber,
            ExpiryDate = card.ExpiryDate,
            Type = card.Type,
            Status = card.Status,
            DailyLimit = card.DailyLimit,
            MonthlyLimit = card.MonthlyLimit,
            IsContactlessEnabled = card.ContactlessEnabled,
            IsOnlineTransactionsEnabled = card.OnlineTransactionsEnabled,
            IsInternationalTransactionsEnabled = card.InternationalTransactionsEnabled,
            CreatedAt = card.CreatedAt
        };
    }

    private async Task<byte[]> GeneratePdfStatementAsync(CardStatement statement, List<CardTransaction> transactions)
    {
        // In real implementation, this would generate a PDF using a library like iTextSharp
        // For now, return a placeholder
        var content = $"PDF Statement for Card ending in {statement.Card?.MaskedCardNumber?.Substring(statement.Card.MaskedCardNumber.Length - 4)}";
        return System.Text.Encoding.UTF8.GetBytes(content);
    }

    private byte[] GenerateCsvStatement(CardStatement statement, List<CardTransaction> transactions)
    {
        var csv = new System.Text.StringBuilder();
        csv.AppendLine("Date,Description,Amount,Status,Reference");
        
        foreach (var transaction in transactions)
        {
            csv.AppendLine($"{transaction.TransactionDate:yyyy-MM-dd},{transaction.MerchantName},{transaction.Amount},{transaction.Status},{transaction.Reference}");
        }
        
        return System.Text.Encoding.UTF8.GetBytes(csv.ToString());
    }

    private async Task<byte[]> GenerateExcelStatementAsync(CardStatement statement, List<CardTransaction> transactions)
    {
        // In real implementation, this would generate an Excel file using a library like EPPlus
        // For now, return CSV content as placeholder
        return GenerateCsvStatement(statement, transactions);
    }

    #endregion
}