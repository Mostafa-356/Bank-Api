using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Payment.Recurring;

public class BulkTransferResult
{
    public Guid BatchId { get; set; }
    public BulkTransferStatus Status { get; set; }
    public int TotalTransfers { get; set; }
    public int SuccessfulTransfers { get; set; }
    public int FailedTransfers { get; set; }
    public decimal TotalAmount { get; set; }
    public List<BulkTransferItemResult> Results { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

