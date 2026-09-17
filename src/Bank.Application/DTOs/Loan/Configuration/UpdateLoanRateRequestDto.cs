namespace Bank.Application.DTOs.Loan.Configuration;

/// <summary>
/// Request DTO for updating loan interest rate
/// </summary>
public class UpdateLoanRateRequest
{
    public decimal NewRate { get; set; }
}

