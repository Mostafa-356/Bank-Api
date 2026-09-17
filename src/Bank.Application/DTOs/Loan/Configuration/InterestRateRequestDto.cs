using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Loan.Configuration;

/// <summary>
/// Request DTO for interest rate inquiry
/// </summary>
public class InterestRateRequest
{
    public LoanType LoanType { get; set; }
    public int CreditScore { get; set; }
    public decimal LoanAmount { get; set; }
}

