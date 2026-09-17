using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Loan.Configuration;

/// <summary>
/// DTO for loan type configuration
/// </summary>
public class LoanTypeConfiguration
{
    public LoanType LoanType { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public decimal MinimumAmount { get; set; }
    public decimal MaximumAmount { get; set; }
    public int MinimumTermMonths { get; set; }
    public int MaximumTermMonths { get; set; }
    public decimal BaseInterestRate { get; set; }
    public InterestCalculationMethod DefaultCalculationMethod { get; set; }
    public bool RequiresCollateral { get; set; }
    public decimal MaxLoanToValueRatio { get; set; }
    public List<string> RequiredDocuments { get; set; } = new();
    public decimal ProcessingFeePercentage { get; set; }
    public decimal PrepaymentPenaltyPercentage { get; set; }
    public bool AllowsEarlyPayoff { get; set; }
}

