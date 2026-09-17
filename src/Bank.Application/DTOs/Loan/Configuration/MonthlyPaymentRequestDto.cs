using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Loan.Configuration;

/// <summary>
/// Request DTO for monthly payment calculation
/// </summary>
public class MonthlyPaymentRequest
{
    public decimal Principal { get; set; }
    public decimal AnnualRate { get; set; }
    public int TermInMonths { get; set; }
    public InterestCalculationMethod CalculationMethod { get; set; } = InterestCalculationMethod.ReducingBalance;
}

