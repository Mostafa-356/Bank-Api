namespace Bank.Application.DTOs.Payment.Batch;

public class BatchPaymentResponse
{
    public string BatchId { get; set; } = string.Empty;
    public int TotalPayments { get; set; }
    public int SuccessfulPayments { get; set; }
    public int FailedPayments { get; set; }
    public DateTime ProcessedDate { get; set; }
    public List<BatchPaymentResult> Results { get; set; } = new();
}

