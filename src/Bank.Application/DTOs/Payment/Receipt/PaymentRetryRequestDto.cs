namespace Bank.Application.DTOs.Payment.Receipt;

public class PaymentRetryRequest
{
    public Guid PaymentId { get; set; }
    public int RetryAttempt { get; set; }
    public string FailureReason { get; set; } = string.Empty;
    public DateTime NextRetryDate { get; set; }
    public TimeSpan BackoffDelay { get; set; }
}

