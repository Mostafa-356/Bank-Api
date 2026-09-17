using Bank.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Bank.Application.DTOs.Card.Operations;

/// <summary>
/// Request for card transaction authorization
/// </summary>
public class CardAuthorizationRequest
{
    [Required]
    public Guid CardId { get; set; }
    
    [Required]
    public decimal Amount { get; set; }
    
    [Required]
    public string Currency { get; set; } = "USD";
    
    [Required]
    public string MerchantId { get; set; } = string.Empty;
    
    [Required]
    public string MerchantName { get; set; } = string.Empty;
    
    public MerchantCategory MerchantCategory { get; set; }
    
    public string? MerchantCountry { get; set; }
    
    public string? TransactionReference { get; set; }
    
    public bool IsInternational { get; set; }
    
    public bool IsOnline { get; set; }
    
    public bool IsContactless { get; set; }
    
    public string? AuthorizationCode { get; set; }
    
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Result of card authorization
/// </summary>
public class CardAuthorizationResult
{
    public bool Success { get; set; }
    public string AuthorizationCode { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public string ResponseCode { get; set; } = string.Empty;
    public string ResponseMessage { get; set; } = string.Empty;
    public decimal AuthorizedAmount { get; set; }
    public DateTime AuthorizationDate { get; set; }
    public string? DeclineReason { get; set; }
    public CardTransactionFees? Fees { get; set; }
}


