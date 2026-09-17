namespace Bank.Application.DTOs.Loan.Configuration;

/// <summary>
/// Request DTO for early payoff calculation
/// </summary>
public class EarlyPayoffRequest
{
    public DateTime PayoffDate { get; set; }
}

