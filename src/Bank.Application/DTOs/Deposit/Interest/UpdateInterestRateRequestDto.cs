namespace Bank.Application.DTOs.Deposit.Interest;

/// <summary>
/// Request to update interest rate for an account
/// </summary>
public class UpdateInterestRateRequest
{
    public Guid AccountId { get; set; }
    public decimal NewInterestRate { get; set; }
    public Guid UserId { get; set; }
}

