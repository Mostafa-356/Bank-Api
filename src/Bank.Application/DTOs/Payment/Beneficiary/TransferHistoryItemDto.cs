using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Payment.Beneficiary;

/// <summary>
/// Transfer history item
/// </summary>
public class TransferHistoryItem
{
    public Guid TransactionId { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransferDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public TransactionStatus Status { get; set; }
}

