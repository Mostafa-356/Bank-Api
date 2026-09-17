namespace Bank.Application.DTOs.Card.Advanced;

/// <summary>
/// Batch settlement result
/// </summary>
public class BatchSettlementResult
{
    public bool Success { get; set; }
    public string BatchId { get; set; } = string.Empty;
    public DateTime ProcessedDate { get; set; }
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public int FailedRecords { get; set; }
    public decimal TotalAmount { get; set; }
    public List<string> Errors { get; set; } = new();
}


