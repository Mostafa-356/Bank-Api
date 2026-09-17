using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Payment.Core;

public record BillerPaymentRequest(
    Guid PaymentId,
    Guid BillerId,
    string BillerAccountNumber,
    string BillerRoutingNumber,
    decimal Amount,
    string Currency,
    string CustomerReference,
    string PaymentDescription,
    DateTime ScheduledDate,
    PaymentMethod PaymentMethod = PaymentMethod.ACH);

