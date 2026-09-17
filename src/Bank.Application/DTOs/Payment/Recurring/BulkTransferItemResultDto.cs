namespace Bank.Application.DTOs.Payment.Recurring;

public class BulkTransferItemResult
{
    public Guid ToAccountId { get; set; }
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? TransactionId { get; set; }
}

