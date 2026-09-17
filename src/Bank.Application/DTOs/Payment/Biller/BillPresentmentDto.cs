using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Payment.Biller;

public class BillPresentmentDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid BillerId { get; set; }
    public string BillerName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public decimal AmountDue { get; set; }
    public decimal MinimumPayment { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime StatementDate { get; set; }
    public string Currency { get; set; } = "USD";
    public BillPresentmentStatus Status { get; set; }
    public string BillNumber { get; set; } = string.Empty;
    public List<BillLineItemDto> LineItems { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidDate { get; set; }
}

