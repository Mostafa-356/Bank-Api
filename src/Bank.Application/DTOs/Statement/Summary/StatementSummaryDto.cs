namespace Bank.Application.DTOs.Statement.Summary;

/// <summary>
/// Statement summary for dashboard
/// </summary>
public class StatementSummary
{
    public Guid AccountId { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public decimal NetChange { get; set; }
    public int TransactionCount { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal TotalFees { get; set; }
    public decimal InterestEarned { get; set; }
    public List<TransactionCategorySummary> CategoryBreakdown { get; set; } = new();
    public List<MonthlyTransactionSummary> MonthlyBreakdown { get; set; } = new();
}

