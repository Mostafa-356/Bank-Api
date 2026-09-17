namespace Bank.Application.DTOs.Account.Transfer;

/// <summary>
/// Daily transfer summary
/// </summary>
public class DailyTransferSummary
{
    public DateTime Date { get; set; }
    public int TransactionCount { get; set; }
    public decimal TotalAmount { get; set; }
}

