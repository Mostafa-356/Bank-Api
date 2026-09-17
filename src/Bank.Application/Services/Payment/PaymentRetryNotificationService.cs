using Bank.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

/// <summary>
/// Service for sending payment retry notifications
/// Centralizes all PaymentRetry notification logic
/// </summary>
public class PaymentRetryNotificationService : IPaymentRetryNotificationService
{
    private readonly ILogger<PaymentRetryNotificationService> _logger;

    public PaymentRetryNotificationService(ILogger<PaymentRetryNotificationService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Notifies when a payment has reached maximum retry attempts
    /// </summary>
    public async Task NotifyPaymentMaxRetriesReached(BillPayment payment)
    {
        try
        {
            _logger.LogWarning("Payment {PaymentId} has reached maximum retry attempts", payment.Id);
            
            // Feature not yet implemented - requires notification service integration
            throw new NotImplementedException("Payment max retries notification is not yet implemented. Requires notification service integration.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending max retries notification for payment {PaymentId}", payment.Id);
            throw;
        }
    }

    /// <summary>
    /// Notifies when a payment has permanently failed after all retry attempts
    /// </summary>
    public async Task NotifyPaymentPermanentFailure(BillPayment payment)
    {
        try
        {
            _logger.LogWarning("Payment {PaymentId} permanently failed after all retry attempts", payment.Id);
            
            // Feature not yet implemented - requires notification service integration
            throw new NotImplementedException("Payment permanent failure notification is not yet implemented. Requires notification service integration.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending permanent failure notification for payment {PaymentId}", payment.Id);
            throw;
        }
    }
}
