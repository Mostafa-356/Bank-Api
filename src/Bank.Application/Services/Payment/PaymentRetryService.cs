using Bank.Application.DTOs;
using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

/// <summary>
/// Service implementation for payment retry logic with exponential backoff
/// </summary>
public class PaymentRetryService : IPaymentRetryService
{
    private readonly IPaymentRetryRepository _paymentRetryRepository;
    private readonly IBillPaymentRepository _billPaymentRepository;
    private readonly IBillerIntegrationService _billerIntegrationService;
    private readonly INotificationService _notificationService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentRetryService> _logger;

    // Configuration constants
    private readonly int _maxRetryAttempts;
    private readonly TimeSpan _baseRetryDelay;
    private readonly TimeSpan _maxRetryDelay;

    public PaymentRetryService(
        IPaymentRetryRepository paymentRetryRepository,
        IBillPaymentRepository billPaymentRepository,
        IBillerIntegrationService billerIntegrationService,
        INotificationService notificationService,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<PaymentRetryService> logger)
    {
        _paymentRetryRepository = paymentRetryRepository;
        _billPaymentRepository = billPaymentRepository;
        _billerIntegrationService = billerIntegrationService;
        _notificationService = notificationService;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _logger = logger;

        // Load configuration with defaults
        _maxRetryAttempts = int.TryParse(_configuration["PaymentRetry:MaxAttempts"], out var maxAttempts) ? maxAttempts : 3;
        _baseRetryDelay = TimeSpan.FromMinutes(int.TryParse(_configuration["PaymentRetry:BaseDelayMinutes"], out var baseDelay) ? baseDelay : 5);
        _maxRetryDelay = TimeSpan.FromHours(int.TryParse(_configuration["PaymentRetry:MaxDelayHours"], out var maxDelay) ? maxDelay : 24);
    }

    public async Task<PaymentRetryResult> SchedulePaymentRetryAsync(PaymentRetryRequest request)
    {
        try
        {
            var payment = await _billPaymentRepository.GetByIdAsync(request.PaymentId);
            if (payment == null)
            {
                return new PaymentRetryResult
                {
                    PaymentId = request.PaymentId, Success = false,
                    Message = "Payment not found"
                };
            }

            // Check if payment has already reached max retries
            var existingRetries = await _paymentRetryRepository.GetPaymentRetriesAsync(request.PaymentId);
            if (existingRetries.Count >= _maxRetryAttempts)
            {
                return new PaymentRetryResult
                {
                    PaymentId = request.PaymentId, Success = false,
                    Message = "Maximum retry attempts reached",
                    IsMaxRetriesReached = true
                };
            }

            var attemptNumber = existingRetries.Count + 1;
            var nextRetryDate = PaymentRetry.CalculateNextRetryDate(attemptNumber, _baseRetryDelay);
            
            // Cap the retry date to max delay
            if (nextRetryDate > DateTime.UtcNow.Add(_maxRetryDelay))
            {
                nextRetryDate = DateTime.UtcNow.Add(_maxRetryDelay);
            }

            var paymentRetry = new PaymentRetry
            {
                PaymentId = request.PaymentId,
                AttemptNumber = attemptNumber,
                AttemptDate = DateTime.UtcNow,
                NextRetryDate = nextRetryDate,
                BackoffDelay = nextRetryDate - DateTime.UtcNow,
                FailureReason = request.FailureReason,
                Status = BillPaymentStatus.Pending,
                IsMaxRetriesReached = attemptNumber >= _maxRetryAttempts
            };

            await _paymentRetryRepository.AddAsync(paymentRetry);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Payment retry scheduled: {PaymentId}, Attempt: {AttemptNumber}, Next retry: {NextRetryDate}", 
                request.PaymentId, attemptNumber, nextRetryDate);

            // Send notification if this is the last retry attempt
            if (paymentRetry.IsMaxRetriesReached)
            {
                await NotifyMaxRetriesReached(payment);
            }

            return new PaymentRetryResult
            {
                PaymentId = request.PaymentId, Success = true,
                AttemptNumber = attemptNumber,
                AttemptDate = DateTime.UtcNow,
                NextRetryDate = nextRetryDate,
                IsMaxRetriesReached = paymentRetry.IsMaxRetriesReached,
                Message = paymentRetry.IsMaxRetriesReached 
                    ? "Final retry attempt scheduled" 
                    : $"Retry attempt {attemptNumber} scheduled"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scheduling retry for payment {PaymentId}", request.PaymentId);
            
            return new PaymentRetryResult
            {
                PaymentId = request.PaymentId, Success = false,
                Message = $"Failed to schedule retry: {ex.Message}"
            };
        }
    }

    public async Task<List<PaymentRetryResult>> ProcessRetryPaymentsAsync()
    {
        var results = new List<PaymentRetryResult>();
        
        try
        {
            var retryPayments = await _paymentRetryRepository.GetPaymentsDueForRetryAsync();
            
            _logger.LogInformation("Processing {Count} payments due for retry", retryPayments.Count);

            foreach (var retry in retryPayments)
            {
                var result = await ProcessSingleRetryPayment(retry);
                results.Add(result);
            }

            if (retryPayments.Any())
            {
                await _unitOfWork.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing retry payments");
        }

        return results;
    }

    public async Task<List<PaymentRetryResult>> GetPaymentRetryHistoryAsync(Guid paymentId)
    {
        try
        {
            var retries = await _paymentRetryRepository.GetPaymentRetriesAsync(paymentId);
            
            return retries.Select(r => new PaymentRetryResult
            {
                PaymentId = r.PaymentId, Success = r.Status == BillPaymentStatus.Processed,
                AttemptNumber = r.AttemptNumber,
                AttemptDate = r.AttemptDate,
                NextRetryDate = r.NextRetryDate,
                IsMaxRetriesReached = r.IsMaxRetriesReached,
                Message = GetRetryStatusMessage(r.Status, r.FailureReason)
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting retry history for payment {PaymentId}", paymentId);
            return new List<PaymentRetryResult>();
        }
    }

    public async Task<bool> CancelPaymentRetriesAsync(Guid paymentId)
    {
        try
        {
            var retries = await _paymentRetryRepository.GetPaymentRetriesAsync(paymentId);
            var pendingRetries = retries.Where(r => r.Status == BillPaymentStatus.Pending).ToList();

            foreach (var retry in pendingRetries)
            {
                retry.MarkAsCompleted(BillPaymentStatus.Cancelled);
                _paymentRetryRepository.Update(retry);
            }

            if (pendingRetries.Any())
            {
                await _unitOfWork.SaveChangesAsync();
                _logger.LogInformation("Cancelled {Count} pending retries for payment {PaymentId}", 
                    pendingRetries.Count, paymentId);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling retries for payment {PaymentId}", paymentId);
            return false;
        }
    }

    public async Task<Dictionary<string, int>> GetRetryStatisticsAsync(DateTime fromDate, DateTime toDate)
    {
        try
        {
            return await _paymentRetryRepository.GetRetryStatisticsAsync(fromDate, toDate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting retry statistics");
            return new Dictionary<string, int>();
        }
    }

    public async Task<List<Guid>> ProcessMaxRetriesReachedAsync()
    {
        var processedPaymentIds = new List<Guid>();

        try
        {
            var maxRetriesReached = await _paymentRetryRepository.GetMaxRetriesReachedAsync();
            
            foreach (var retry in maxRetriesReached)
            {
                var payment = await _billPaymentRepository.GetByIdAsync(retry.PaymentId);
                if (payment != null && payment.Status != BillPaymentStatus.Failed)
                {
                    // Mark payment as permanently failed
                    payment.MarkAsFailed();
                    _billPaymentRepository.Update(payment);
                    
                    // Send failure notification
                    await NotifyPaymentPermanentlyFailed(payment);
                    
                    processedPaymentIds.Add(payment.Id);
                    
                    _logger.LogWarning("Payment {PaymentId} marked as permanently failed after {MaxAttempts} retry attempts", 
                        payment.Id, _maxRetryAttempts);
                }
            }

            if (processedPaymentIds.Any())
            {
                await _unitOfWork.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing max retries reached payments");
        }

        return processedPaymentIds;
    }

    public async Task<bool> HasReachedMaxRetriesAsync(Guid paymentId)
    {
        try
        {
            var retries = await _paymentRetryRepository.GetPaymentRetriesAsync(paymentId);
            return retries.Count >= _maxRetryAttempts || retries.Any(r => r.IsMaxRetriesReached);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking max retries for payment {PaymentId}", paymentId);
            return false;
        }
    }

    #region Private Helper Methods

    private async Task<PaymentRetryResult> ProcessSingleRetryPayment(PaymentRetry retry)
    {
        try
        {
            var payment = await _billPaymentRepository.GetPaymentWithDetailsAsync(retry.PaymentId);
            if (payment == null)
            {
                retry.MarkAsCompleted(BillPaymentStatus.Failed);
                _paymentRetryRepository.Update(retry);
                
                return new PaymentRetryResult
                {
                    PaymentId = retry.PaymentId, Success = false,
                    AttemptNumber = retry.AttemptNumber,
                    AttemptDate = DateTime.UtcNow,
                    Message = "Payment not found"
                };
            }

            // Create biller payment request
            var billerRequest = new BillerPaymentRequest(
                payment.Id,
                payment.BillerId,
                payment.Biller.AccountNumber,
                payment.Biller.RoutingNumber,
                payment.Amount,
                payment.Currency,
                payment.Reference,
                payment.Description,
                payment.ScheduledDate
            );

            // Attempt to send payment to biller
            var billerResponse = await _billerIntegrationService.SendPaymentToBillerAsync(billerRequest);

            if (billerResponse.Success)
            {
                // Update payment status
                payment.MarkAsProcessed();
                _billPaymentRepository.Update(payment);

                // Mark retry as successful
                retry.MarkAsCompleted(BillPaymentStatus.Processed);
                _paymentRetryRepository.Update(retry);

                _logger.LogInformation("Retry successful for payment {PaymentId}, attempt {AttemptNumber}", 
                    retry.PaymentId, retry.AttemptNumber);

                return new PaymentRetryResult
                {
                    PaymentId = retry.PaymentId, Success = true,
                    AttemptNumber = retry.AttemptNumber,
                    AttemptDate = DateTime.UtcNow,
                    Message = "Retry successful"
                };
            }
            else
            {
                // Check if we should schedule another retry
                if (retry.AttemptNumber < _maxRetryAttempts)
                {
                    // Schedule next retry
                    var nextRetryRequest = new PaymentRetryRequest
                    {
                        PaymentId = retry.PaymentId,
                        RetryAttempt = retry.AttemptNumber + 1,
                        FailureReason = billerResponse.Message
                    };

                    await SchedulePaymentRetryAsync(nextRetryRequest);
                }
                else
                {
                    // Mark as max retries reached
                    retry.MarkAsMaxRetriesReached();
                    _paymentRetryRepository.Update(retry);
                }

                retry.MarkAsCompleted(BillPaymentStatus.Failed);
                _paymentRetryRepository.Update(retry);

                return new PaymentRetryResult
                {
                    PaymentId = retry.PaymentId, Success = false,
                    AttemptNumber = retry.AttemptNumber,
                    AttemptDate = DateTime.UtcNow,
                    IsMaxRetriesReached = retry.AttemptNumber >= _maxRetryAttempts,
                    Message = billerResponse.Message
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing retry for payment {PaymentId}, attempt {AttemptNumber}", 
                retry.PaymentId, retry.AttemptNumber);

            retry.MarkAsCompleted(BillPaymentStatus.Failed);
            _paymentRetryRepository.Update(retry);

            return new PaymentRetryResult
            {
                PaymentId = retry.PaymentId, Success = false,
                AttemptNumber = retry.AttemptNumber,
                AttemptDate = DateTime.UtcNow,
                Message = $"Retry processing failed: {ex.Message}"
            };
        }
    }

    private async Task NotifyMaxRetriesReached(BillPayment payment)
    {
        await _notificationService.SendSystemNotificationAsync(
            payment.CustomerId,
            "FINAL_RETRY_ATTEMPT",
            $"The final retry attempt for your payment of {payment.Currency} {payment.Amount} to {payment.Biller?.Name} has been scheduled.",
            null);
    }

    private async Task NotifyPaymentPermanentlyFailed(BillPayment payment)
    {
        await _notificationService.SendSystemNotificationAsync(
            payment.CustomerId,
            "PAYMENT_PERMANENT_FAILURE",
            $"Your payment of {payment.Currency} {payment.Amount} to {payment.Biller?.Name} has permanently failed after {_maxRetryAttempts} attempts.",
            null);
    }

    private static string GetRetryStatusMessage(BillPaymentStatus status, string failureReason)
    {
        return status switch
        {
            BillPaymentStatus.Pending => "Retry scheduled",
            BillPaymentStatus.Processing => "Retry in progress",
            BillPaymentStatus.Processed => "Retry successful",
            BillPaymentStatus.Failed => $"Retry failed: {failureReason}",
            BillPaymentStatus.Cancelled => "Retry cancelled",
            _ => "Unknown retry status"
        };
    }

    #endregion
}