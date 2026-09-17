using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Statement.Transaction;

/// <summary>
/// Statement transaction DTO
/// </summary>
public class StatementTransactionDto
{
    public Guid Id { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }
    public decimal Amount { get; set; }
    public decimal RunningBalance { get; set; }
    public TransactionType Type { get; set; }
    public TransactionStatus Status { get; set; }
    public string? Category { get; set; }
    public string? Memo { get; set; }
}

