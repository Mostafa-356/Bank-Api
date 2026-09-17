namespace Bank.Application.DTOs.Statement.Summary;

/// <summary>
/// Transaction category summary
/// </summary>
public class TransactionCategorySummary
{
    public string Category { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal Percentage { get; set; }
}

