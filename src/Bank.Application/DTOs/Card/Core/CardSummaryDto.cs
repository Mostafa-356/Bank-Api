using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Card.Core;

/// <summary>
/// Summary card information for listing
/// </summary>
public class CardSummaryDto
{
    public Guid Id { get; set; }
    public Guid CustomerId { get; set; }
    public Guid AccountId { get; set; }
    public string MaskedCardNumber { get; set; } = string.Empty;
    public CardType Type { get; set; }
    public CardStatus Status { get; set; }
    public DateTime ExpiryDate { get; set; }
    public string? CardName { get; set; }
    public bool IsActive { get; set; }
    public bool IsExpired { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public decimal DailyLimit { get; set; }
    public decimal MonthlyLimit { get; set; }
    public bool IsContactlessEnabled { get; set; }
    public bool IsOnlineTransactionsEnabled { get; set; }
    public bool IsInternationalTransactionsEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
}


