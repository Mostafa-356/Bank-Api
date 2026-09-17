using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Payment.Biller;

// Biller DTOs
public record CreateBillerRequest(
    string Name,
    BillerCategory Category,
    string AccountNumber,
    string RoutingNumber,
    string Address,
    string[] SupportedPaymentMethods,
    decimal MinAmount,
    decimal MaxAmount,
    int ProcessingDays);

public record UpdateBillerRequest(
    string Name,
    BillerCategory Category,
    string AccountNumber,
    string RoutingNumber,
    string Address,
    string[] SupportedPaymentMethods,
    decimal MinAmount,
    decimal MaxAmount,
    int ProcessingDays,
    bool IsActive);

public class BillerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public BillerCategory Category { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string RoutingNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string[] SupportedPaymentMethods { get; set; } = Array.Empty<string>();
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public int ProcessingDays { get; set; }
    public DateTime CreatedAt { get; set; }
}

// Bill Payment DTOs
public record ScheduleBillPaymentRequest(
    Guid BillerId,
    decimal Amount,
    string Currency,
    DateTime ScheduledDate,
    string? Reference,
    string? Description);

public record UpdateBillPaymentRequest(
    decimal Amount,
    DateTime ScheduledDate,
    string? Reference,
    string? Description);

public class BillPaymentDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid BillerId { get; set; }
    public string BillerName { get; set; } = string.Empty;
    public BillerCategory BillerCategory { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public DateTime? ProcessedDate { get; set; }
    public BillPaymentStatus Status { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid? RecurringPaymentId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class BillPaymentHistoryDto
{
    public Guid Id { get; set; }
    public string BillerName { get; set; } = string.Empty;
    public BillerCategory BillerCategory { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime ScheduledDate { get; set; }
    public DateTime? ProcessedDate { get; set; }
    public BillPaymentStatus Status { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsRecurring { get; set; }
}

// Search and Filter DTOs
public record BillerSearchRequest(
    string? SearchTerm,
    BillerCategory? Category,
    bool ActiveOnly = true);

public record BillPaymentHistoryRequest(
    int PageNumber = 1,
    int PageSize = 20,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    BillPaymentStatus? Status = null);

// Response DTOs
public class ScheduleBillPaymentResponse
{
    public Guid PaymentId { get; set; }
    public BillPaymentStatus Status { get; set; }
    public DateTime ScheduledDate { get; set; }
    public DateTime ExpectedProcessingDate { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ProcessBillPaymentResponse
{
    public Guid PaymentId { get; set; }
    public BillPaymentStatus Status { get; set; }
    public DateTime? ProcessedDate { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool Success { get; set; }
}

