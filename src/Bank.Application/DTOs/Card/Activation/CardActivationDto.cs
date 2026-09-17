using Bank.Application.DTOs.Common;
using Bank.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Bank.Application.DTOs.Card.Activation;

/// <summary>
/// Request to activate a card
/// </summary>
public class CardActivationRequest
{
    [Required]
    public Guid CardId { get; set; }
    
    [Required]
    public Guid CustomerId { get; set; }
    
    [Required]
    public string ActivationCode { get; set; } = string.Empty;
    
    [Required]
    public CardActivationChannel Channel { get; set; }
    
    public string? Pin { get; set; }
}

/// <summary>
/// Result of card activation operation
/// </summary>
public class CardActivationResult : BaseResultDto
{
    public DateTime? ActivationDate { get; set; }
}


