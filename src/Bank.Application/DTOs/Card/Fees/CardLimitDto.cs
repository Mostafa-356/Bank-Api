using Bank.Application.DTOs.Common;
using System.ComponentModel.DataAnnotations;

namespace Bank.Application.DTOs.Card.Fees;

/// <summary>
/// Request to update card limits
/// </summary>
public class CardLimitUpdateRequest
{
    [Required]
    public Guid CardId { get; set; }
    
    [Required]
    public Guid CustomerId { get; set; }
    
    [Range(0, 100000)]
    public decimal? DailyLimit { get; set; }
    
    [Range(0, 1000000)]
    public decimal? MonthlyLimit { get; set; }
    
    [Range(0, 50000)]
    public decimal? AtmDailyLimit { get; set; }
}

/// <summary>
/// Result of card limit update operation
/// </summary>
public class CardLimitUpdateResult : BaseResultDto
{
    public decimal? NewDailyLimit { get; set; }
    public decimal? NewMonthlyLimit { get; set; }
    public decimal? NewAtmDailyLimit { get; set; }
}


