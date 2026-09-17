using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Card.Core;

/// <summary>
/// Detailed card information
/// </summary>
public class CardDetailsDto
{
    public Guid Id { get; set; }
    public string MaskedCardNumber { get; set; } = string.Empty;
    public CardType Type { get; set; }
    public CardStatus Status { get; set; }
    public DateTime ExpiryDate { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime? ActivationDate { get; set; }
    public CardActivationChannel? ActivationChannel { get; set; }
    public decimal DailyLimit { get; set; }
    public decimal MonthlyLimit { get; set; }
    public decimal AtmDailyLimit { get; set; }
    public bool ContactlessEnabled { get; set; }
    public bool OnlineTransactionsEnabled { get; set; }
    public bool InternationalTransactionsEnabled { get; set; }
    public string? CardName { get; set; }
    public List<MerchantCategory> BlockedMerchantCategories { get; set; } = new();
    public DateTime? LastBlockedDate { get; set; }
    public CardBlockReason? LastBlockReason { get; set; }
    public bool HasPin { get; set; }
    public DateTime? PinSetDate { get; set; }
    public AccountSummaryDto Account { get; set; } = null!;
}


