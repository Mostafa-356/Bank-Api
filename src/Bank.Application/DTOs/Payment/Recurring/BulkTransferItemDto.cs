namespace Bank.Application.DTOs.Payment.Recurring;

public class BulkTransferItem
{
    public Guid ToAccountId { get; set; }
    public decimal Amount { get; set; }
    public string? Reference { get; set; }
    public string? Description { get; set; }
}

