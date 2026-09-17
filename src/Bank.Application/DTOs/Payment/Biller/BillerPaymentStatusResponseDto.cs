using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Payment.Biller;

public class BillerPaymentStatusResponse
{
    public string ExternalReference { get; set; } = string.Empty;
    public BillPaymentStatus Status { get; set; }
    public DateTime LastUpdated { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public DateTime? DeliveredDate { get; set; }
    public string? FailureReason { get; set; }
}

