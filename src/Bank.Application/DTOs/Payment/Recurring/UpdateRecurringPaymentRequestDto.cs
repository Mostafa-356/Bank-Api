using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Payment.Recurring;

public class UpdateRecurringPaymentRequest
{
    public decimal? Amount { get; set; }
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public RecurringPaymentFrequency? Frequency { get; set; }
    public DateTime? EndDate { get; set; }
    public int? MaxOccurrences { get; set; }
    public int? MaxRetries { get; set; }
}

