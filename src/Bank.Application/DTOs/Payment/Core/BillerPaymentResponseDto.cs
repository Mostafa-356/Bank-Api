using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Payment.Core;

public class BillerPaymentResponse
{
    public bool Success { get; set; }
    public string ExternalReference { get; set; } = string.Empty;
    public string ConfirmationNumber { get; set; } = string.Empty;
    public BillPaymentStatus Status { get; set; }
    public DateTime ProcessedDate { get; set; }
    public string Message { get; set; } = string.Empty;
    public decimal? ProcessingFee { get; set; }
    public DateTime? ExpectedDeliveryDate { get; set; }
}

