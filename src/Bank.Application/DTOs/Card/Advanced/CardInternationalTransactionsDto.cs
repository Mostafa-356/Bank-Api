using Bank.Application.DTOs.Common;
using System.ComponentModel.DataAnnotations;

namespace Bank.Application.DTOs.Card.Advanced;

/// <summary>
/// Request to update international transactions settings
/// </summary>
public class CardInternationalTransactionsRequest
{
    [Required]
    public Guid CardId { get; set; }
    
    [Required]
    public Guid CustomerId { get; set; }
    
    [Required]
    public bool Enabled { get; set; }
}

/// <summary>
/// Result of international transactions settings update
/// </summary>
public class CardInternationalTransactionsResult : BaseResultDto
{
    public bool InternationalTransactionsEnabled { get; set; }
}


