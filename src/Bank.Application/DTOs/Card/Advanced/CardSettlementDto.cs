using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Card.Advanced;

/// <summary>
/// Request for settlement processing
/// </summary>
public class CardSettlementRequest
{
    public DateTime SettlementDate { get; set; } = DateTime.UtcNow.Date;
    public List<string>? TransactionIds { get; set; }
    public CardNetwork? Network { get; set; }
}

/// <summary>
/// Result of settlement processing
/// </summary>
public class CardSettlementResult
{
    public bool Success { get; set; }
    public string SettlementId { get; set; } = string.Empty;
    public DateTime SettlementDate { get; set; }
    public int TransactionCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalFees { get; set; }
    public decimal NetAmount { get; set; }
    public List<string> ProcessedTransactions { get; set; } = new();
    public List<string> FailedTransactions { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Settlement report
/// </summary>
public class CardSettlementReport
{
    public DateTime SettlementDate { get; set; }
    public List<CardSettlementSummary> NetworkSummaries { get; set; } = new();
    public decimal TotalSettlementAmount { get; set; }
    public decimal TotalFees { get; set; }
    public decimal NetSettlementAmount { get; set; }
    public int TotalTransactionCount { get; set; }
}

/// <summary>
/// Settlement summary by network
/// </summary>
public class CardSettlementSummary
{
    public CardNetwork Network { get; set; }
    public int TransactionCount { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal InterchangeFees { get; set; }
    public decimal ProcessingFees { get; set; }
    public decimal NetAmount { get; set; }
}


