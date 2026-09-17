using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Payment.Routing;

public class PaymentStatusSyncResult
{
    public Guid PaymentId { get; set; }
    public bool Success { get; set; }
    public BillPaymentStatus OldStatus { get; set; }
    public BillPaymentStatus NewStatus { get; set; }
    public DateTime SyncDate { get; set; }
    public string Message { get; set; } = string.Empty;
}

