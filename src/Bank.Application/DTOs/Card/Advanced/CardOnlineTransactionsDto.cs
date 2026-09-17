using Bank.Application.DTOs.Common;
using System.ComponentModel.DataAnnotations;

namespace Bank.Application.DTOs.Card.Advanced;

/// <summary>
/// Request to update online transactions settings
/// </summary>
public class CardOnlineTransactionsRequest
{
    [Required]
    public Guid CardId { get; set; }
    
    [Required]
    public Guid CustomerId { get; set; }
    
    [Required]
    public bool Enabled { get; set; }
}

/// <summary>
/// Result of online transactions settings update
/// </summary>
public class CardOnlineTransactionsResult : BaseResultDto
{
    public bool OnlineTransactionsEnabled { get; set; }
}


