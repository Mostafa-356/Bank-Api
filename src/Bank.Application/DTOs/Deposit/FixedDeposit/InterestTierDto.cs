using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Deposit.FixedDeposit;

/// <summary>
/// Interest tier data transfer object
/// </summary>
public class InterestTierDto
{
    public Guid Id { get; set; }
    public Guid DepositProductId { get; set; }
    public string TierName { get; set; } = string.Empty;
    public decimal MinimumBalance { get; set; }
    public decimal? MaximumBalance { get; set; }
    public decimal InterestRate { get; set; }
    public InterestTierBasis TierBasis { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
    
    public DateTime? EffectiveFromDate { get; set; }
    public DateTime? EffectiveToDate { get; set; }
    public bool IsPromotional { get; set; }
    public bool IsEffective { get; set; }
}

/// <summary>
/// Request to create an interest tier
/// </summary>
public class CreateInterestTierRequest
{
    public string TierName { get; set; } = string.Empty;
    public decimal MinimumBalance { get; set; }
    public decimal? MaximumBalance { get; set; }
    public decimal InterestRate { get; set; }
    public InterestTierBasis TierBasis { get; set; }
    public int DisplayOrder { get; set; }
    
    public DateTime? EffectiveFromDate { get; set; }
    public DateTime? EffectiveToDate { get; set; }
    public bool IsPromotional { get; set; }
}

/// <summary>
/// Request to update an interest tier
/// </summary>
public class UpdateInterestTierRequest
{
    public string? TierName { get; set; }
    public decimal? InterestRate { get; set; }
    public bool? IsActive { get; set; }
    public int? DisplayOrder { get; set; }
    
    public DateTime? EffectiveFromDate { get; set; }
    public DateTime? EffectiveToDate { get; set; }
}


