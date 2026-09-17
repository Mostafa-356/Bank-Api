using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Deposit.Core;

/// <summary>
/// Deposit product data transfer object
/// </summary>
public class DepositProductDto : BaseDepositProductDto
{
    public Guid Id { get; set; }
    public bool IsActive { get; set; }
    public bool IsPromotionalRateActive { get; set; }
    
    public List<InterestTierDto> InterestTiers { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Request to create a new deposit product
/// </summary>
public class CreateDepositProductRequest : BaseDepositProductDto
{
}

/// <summary>
/// Request to update a deposit product
/// </summary>
public class UpdateDepositProductRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public bool? IsActive { get; set; }
    
    public decimal? BaseInterestRate { get; set; }
    public bool? AllowPartialWithdrawals { get; set; }
    public WithdrawalPenaltyType? PenaltyType { get; set; }
    public decimal? PenaltyAmount { get; set; }
    public decimal? PenaltyPercentage { get; set; }
    
    public MaturityAction? DefaultMaturityAction { get; set; }
    public bool? AllowAutoRenewal { get; set; }
    public int? AutoRenewalNoticeDays { get; set; }
    
    public DateTime? PromotionalRateStartDate { get; set; }
    public DateTime? PromotionalRateEndDate { get; set; }
    public decimal? PromotionalRate { get; set; }
}


