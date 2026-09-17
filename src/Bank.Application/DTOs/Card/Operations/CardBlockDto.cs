using Bank.Application.DTOs.Common;
using Bank.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Bank.Application.DTOs.Card.Operations;

/// <summary>
/// Request to block a card
/// </summary>
public class CardBlockRequest
{
    [Required]
    public Guid CardId { get; set; }
    
    [Required]
    public Guid CustomerId { get; set; }
    
    [Required]
    public CardBlockReason Reason { get; set; }
    
    public string? Notes { get; set; }
}

/// <summary>
/// Request to unblock a card
/// </summary>
public class CardUnblockRequest
{
    [Required]
    public Guid CardId { get; set; }
    
    [Required]
    public Guid CustomerId { get; set; }
    
    public string? Notes { get; set; }
}

/// <summary>
/// Result of card block/unblock operation
/// </summary>
public class CardBlockResult : BaseResultDto
{
    public CardStatus? NewStatus { get; set; }
    public DateTime? StatusChangeDate { get; set; }
}



