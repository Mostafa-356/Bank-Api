using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Deposit.FixedDeposit;

/// <summary>
/// Base fixed deposit data transfer object with common properties
/// </summary>
public abstract class BaseFixedDepositDto
{
    // Identity
    public Guid Id { get; set; }
    public string DepositNumber { get; set; } = string.Empty;
    
    // Customer & Product
    public Guid CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public Guid DepositProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    
    // Account
    public Guid LinkedAccountId { get; set; }
    public string LinkedAccountNumber { get; set; } = string.Empty;
    
    // Core Terms
    public decimal PrincipalAmount { get; set; }
    public decimal InterestRate { get; set; }
    public int TermDays { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime MaturityDate { get; set; }
    public FixedDepositStatus Status { get; set; }
    
    // Interest Configuration
    public InterestCalculationMethod InterestCalculationMethod { get; set; }
    public InterestCompoundingFrequency CompoundingFrequency { get; set; }
    public decimal AccruedInterest { get; set; }
    public DateTime LastInterestCalculationDate { get; set; }
    
    // Maturity Settings
    public MaturityAction MaturityAction { get; set; }
    public bool AutoRenewalEnabled { get; set; }
    public int? RenewalTermDays { get; set; }
    public DateTime? RenewalNoticeDate { get; set; }
    public bool CustomerConsentReceived { get; set; }
    
    // Penalty Configuration
    public WithdrawalPenaltyType PenaltyType { get; set; }
    public decimal? PenaltyAmount { get; set; }
    public decimal? PenaltyPercentage { get; set; }
    
    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>
/// Fixed deposit data transfer object with full details including closure info
/// </summary>
public class FixedDepositDto : BaseFixedDepositDto
{
    // Closure Information
    public DateTime? ClosureDate { get; set; }
    public string? ClosureReason { get; set; }
    public decimal? PenaltyApplied { get; set; }
    public decimal? NetAmountPaid { get; set; }
    
    // Computed Fields
    public int RenewalCount { get; set; }
    public decimal MaturityAmount { get; set; }
    public decimal InterestAtMaturity { get; set; }
    public int DaysToMaturity { get; set; }
    public bool HasMatured { get; set; }
}

/// <summary>
/// Request to create a fixed deposit
/// </summary>
public class CreateFixedDepositRequest
{
    public Guid DepositProductId { get; set; }
    public Guid LinkedAccountId { get; set; }
    public decimal PrincipalAmount { get; set; }
    public int? TermDays { get; set; }
    public MaturityAction? MaturityAction { get; set; }
    public bool? AutoRenewalEnabled { get; set; }
    public int? RenewalTermDays { get; set; }
}

/// <summary>
/// Request to renew a fixed deposit
/// </summary>
public class RenewDepositRequest
{
    public int? TermDays { get; set; }
    public decimal? InterestRate { get; set; }
    public MaturityAction? MaturityAction { get; set; }
    public bool? AutoRenewalEnabled { get; set; }
}
