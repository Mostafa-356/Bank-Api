using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Payment.Recurring;

public class CreateRecurringPaymentRequest
{
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public RecurringPaymentFrequency Frequency { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? MaxOccurrences { get; set; }
    public int MaxRetries { get; set; } = 3;
    public Guid CreatedByUserId { get; set; }
}

