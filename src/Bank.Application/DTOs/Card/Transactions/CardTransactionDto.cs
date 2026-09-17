using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Card.Transactions;

/// <summary>
/// Card transaction information
/// </summary>
public class CardTransactionDto
{
    public Guid Id { get; set; }
    public Guid CardId { get; set; }
    public string NetworkTransactionId { get; set; } = string.Empty;
    public string? AuthorizationCode { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public decimal? OriginalAmount { get; set; }
    public string? OriginalCurrencyCode { get; set; }
    public CardTransactionType Type { get; set; }
    public CardTransactionType TransactionType { get; set; }
    public CardTransactionStatus Status { get; set; }
    public DateTime TransactionDate { get; set; }
    public DateTime? SettlementDate { get; set; }
    public string? MerchantName { get; set; }
    public MerchantCategory? MerchantCategory { get; set; }
    public string? MerchantCity { get; set; }
    public string? MerchantCountryCode { get; set; }
    public bool IsContactless { get; set; }
    public bool IsOnline { get; set; }
    public bool IsInternational { get; set; }
    public string? Description { get; set; }
    public string? Reference { get; set; }
    public decimal Fee { get; set; }
    public decimal? BalanceAfterTransaction { get; set; }
}


