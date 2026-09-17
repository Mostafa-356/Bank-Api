using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Payment.Receipt;

public class PaymentReceiptDto
{
    public Guid PaymentId { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string BillerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public DateTime ProcessedDate { get; set; }
    public string ConfirmationNumber { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public PaymentMethod PaymentMethod { get; set; }
    public decimal? ProcessingFee { get; set; }
    public BillPaymentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

