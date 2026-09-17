using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Loan.Configuration;

/// <summary>
/// DTO for loan interest calculation result
/// </summary>
public class LoanInterestCalculationResult
{
    public decimal InterestAmount { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal TotalPayment { get; set; }
    public decimal RemainingBalance { get; set; }
    public InterestCalculationMethod CalculationMethod { get; set; }
    public DateTime CalculationDate { get; set; }
    public int DaysCalculated { get; set; }
}

