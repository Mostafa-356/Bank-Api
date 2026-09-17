using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Payment.Batch;

public class BatchPaymentResult
{
    public Guid PaymentId { get; set; }
    public bool Success { get; set; }
    public string ExternalReference { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public BillPaymentStatus Status { get; set; }
}

