using Bank.Application.DTOs.Common;

namespace Bank.Application.DTOs.Deposit.Interest;

/// <summary>
/// Result of interest calculation
/// </summary>
public class InterestCalculationResult : BaseResultDto
{
    public decimal InterestAmount { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal InterestRate { get; set; }
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int DaysCalculated { get; set; }
    public InterestCompoundingFrequency CompoundingFrequency { get; set; }
}


