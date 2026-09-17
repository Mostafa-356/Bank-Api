namespace Bank.Application.DTOs.Payment.Receipt;

public class PaymentRetryResult
{
    public Guid PaymentId { get; set; }
    public bool Success { get; set; }
    public int AttemptNumber { get; set; }
    public DateTime AttemptDate { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime? NextRetryDate { get; set; }
    public bool IsMaxRetriesReached { get; set; }
}

