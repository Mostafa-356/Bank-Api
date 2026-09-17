using Bank.Application.DTOs.Common;
using Bank.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Bank.Application.DTOs.Card.Advanced;

/// <summary>
/// Request to update merchant restrictions
/// </summary>
public class CardMerchantRestrictionsRequest
{
    [Required]
    public Guid CardId { get; set; }
    
    [Required]
    public Guid CustomerId { get; set; }
    
    public List<MerchantCategory> BlockedCategories { get; set; } = new();
}

/// <summary>
/// Result of merchant restrictions update
/// </summary>
public class CardMerchantRestrictionsResult : BaseResultDto
{
    public List<MerchantCategory> BlockedCategories { get; set; } = new();
}


