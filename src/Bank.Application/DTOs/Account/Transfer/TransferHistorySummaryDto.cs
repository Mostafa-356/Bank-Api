namespace Bank.Application.DTOs.Account.Transfer;

/// <summary>
/// Transfer history summary for limit calculations
/// </summary>
public class TransferHistorySummary
{
    public Guid BeneficiaryId { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int TransactionCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal LargestTransfer { get; set; }
    public DateTime? LastTransferDate { get; set; }
    public List<DailyTransferSummary> DailySummaries { get; set; } = new();
}

