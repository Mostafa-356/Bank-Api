using Bank.Application.DTOs.Statement.Summary;

namespace Bank.Application.DTOs.Statement.Analytics;

/// <summary>
/// Statement statistics for analytics
/// </summary>
public class StatementStatistics
{
    public Guid AccountId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int TransactionCount { get; set; }
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetChange { get; set; }
    public List<TransactionCategorySummary> CategoryBreakdown { get; set; } = new();
    public List<MonthlyTransactionSummary> MonthlyBreakdown { get; set; } = new();
}

