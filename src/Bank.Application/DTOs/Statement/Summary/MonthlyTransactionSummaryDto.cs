namespace Bank.Application.DTOs.Statement.Summary;

/// <summary>
/// Monthly transaction summary
/// </summary>
public class MonthlyTransactionSummary
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal TotalDebits { get; set; }
    public decimal TotalCredits { get; set; }
    public decimal NetAmount { get; set; }
    public decimal AverageBalance { get; set; }
}

