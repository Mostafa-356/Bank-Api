namespace Bank.Application.DTOs.Payment.Recurring;

public class BulkTransferRequest
{
    public Guid FromAccountId { get; set; }
    public List<BulkTransferItem> Transfers { get; set; } = new();
    public string Description { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
}

