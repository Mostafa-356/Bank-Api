using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Statement.Delivery;

/// <summary>
/// Consolidated statement request for multiple accounts
/// </summary>
public class ConsolidatedStatementRequest
{
    public List<Guid> AccountIds { get; set; } = new();
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public StatementFormat Format { get; set; } = StatementFormat.PDF;
    public StatementDeliveryMethod DeliveryMethod { get; set; } = StatementDeliveryMethod.Email;
    public string? EmailAddress { get; set; }
    public bool IncludeAccountSummaries { get; set; } = true;
    public bool IncludeConsolidatedSummary { get; set; } = true;
    public bool IncludeTransactionDetails { get; set; } = true;
    public string? CustomTitle { get; set; }
}

