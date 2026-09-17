using Bank.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Bank.Application.DTOs.Card.Operations;

/// <summary>
/// Request to validate card for transaction
/// </summary>
public class CardValidationRequest
{
    [Required]
    public Guid CardId { get; set; }
    
    [Required]
    public decimal Amount { get; set; }
    
    [Required]
    public CardTransactionType TransactionType { get; set; }
    
    public MerchantCategory? MerchantCategory { get; set; }
    
    public bool IsInternational { get; set; } = false;
    
    public bool IsOnline { get; set; } = false;
    
    public string? Pin { get; set; }
}

/// <summary>
/// Result of card validation
/// </summary>
public class CardValidationResult
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public string ResponseCode { get; set; } = string.Empty;
    public string ResponseMessage { get; set; } = string.Empty;
    public string? DeclineReason { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public decimal? AvailableBalance { get; set; }
    public decimal? DailyLimitRemaining { get; set; }
    public decimal? MonthlyLimitRemaining { get; set; }
}


