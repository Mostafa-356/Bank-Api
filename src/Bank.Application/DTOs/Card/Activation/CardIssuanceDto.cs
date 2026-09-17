using Bank.Application.DTOs.Common;
using Bank.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Bank.Application.DTOs.Card.Activation;

/// <summary>
/// Request to issue a new card
/// </summary>
public class CardIssuanceRequest
{
    [Required]
    public Guid CustomerId { get; set; }
    
    [Required]
    public Guid AccountId { get; set; }
    
    [Required]
    public CardType CardType { get; set; }
    
    public string? CardName { get; set; }
    
    public decimal? DailyLimit { get; set; }
    
    public decimal? MonthlyLimit { get; set; }
    
    public decimal? AtmDailyLimit { get; set; }
    
    public bool ContactlessEnabled { get; set; } = true;
    
    public bool OnlineTransactionsEnabled { get; set; } = true;
    
    public bool InternationalTransactionsEnabled { get; set; } = false;
    
    public List<MerchantCategory>? BlockedMerchantCategories { get; set; }
}

/// <summary>
/// Result of card issuance operation
/// </summary>
public class CardIssuanceResult : BaseResultDto
{
    public Guid? CardId { get; set; }
    public string? MaskedCardNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? ActivationCode { get; set; }
}


