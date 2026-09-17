using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Statement.Core;

/// <summary>
/// Request to generate an account statement
/// </summary>
public class GenerateStatementRequest
{
    public Guid AccountId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public StatementFormat Format { get; set; } = StatementFormat.PDF;
    public StatementDeliveryMethod DeliveryMethod { get; set; } = StatementDeliveryMethod.Email;
    public string? EmailAddress { get; set; }
    public bool IncludeTransactionDetails { get; set; } = true;
    public bool IncludeSummary { get; set; } = true;
    public bool IncludeCharts { get; set; } = false;
    public string? CustomTitle { get; set; }
    public List<TransactionType>? FilterByTransactionTypes { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public string? FilterByDescription { get; set; }
    public List<string>? FilterByCategories { get; set; }
    public string? RequestReason { get; set; }
}

