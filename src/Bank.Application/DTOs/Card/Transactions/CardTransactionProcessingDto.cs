using Bank.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Bank.Application.DTOs.Card.Transactions;

/// <summary>
/// Request for card transaction processing
/// </summary>
public class CardTransactionRequest
{
    [Required]
    public Guid CardId { get; set; }
    
    [Required]
    public decimal Amount { get; set; }
    
    [Required]
    public CardTransactionType TransactionType { get; set; }
    
    [Required]
    public string MerchantId { get; set; } = string.Empty;
    
    [Required]
    public string MerchantName { get; set; } = string.Empty;
    
    public MerchantCategory MerchantCategory { get; set; }
    
    public string Currency { get; set; } = "USD";
    
    public string? Description { get; set; }
    
    public string? Reference { get; set; }
    
    public bool IsInternational { get; set; }
    
    public bool IsOnline { get; set; }
    
    public bool IsContactless { get; set; }
    
    public string? AuthorizationCode { get; set; }
    
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result of card transaction processing
/// </summary>
public class CardTransactionResult
{
    public bool Success { get; set; }
    public Guid TransactionId { get; set; }
    public string AuthorizationCode { get; set; } = string.Empty;
    public CardTransactionStatus Status { get; set; }
    public decimal ProcessedAmount { get; set; }
    public decimal AccountBalance { get; set; }
    public CardTransactionFees? Fees { get; set; }
    public string? ErrorMessage { get; set; }
    public string? DeclineReason { get; set; }
}


