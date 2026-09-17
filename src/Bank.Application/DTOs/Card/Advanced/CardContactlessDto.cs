using Bank.Application.DTOs.Common;
using System.ComponentModel.DataAnnotations;

namespace Bank.Application.DTOs.Card.Advanced;

/// <summary>
/// Request to update contactless settings
/// </summary>
public class CardContactlessRequest
{
    [Required]
    public Guid CardId { get; set; }
    
    [Required]
    public Guid CustomerId { get; set; }
    
    [Required]
    public bool Enabled { get; set; }
}

/// <summary>
/// Result of contactless settings update
/// </summary>
public class CardContactlessResult : BaseResultDto
{
    public bool ContactlessEnabled { get; set; }
}


