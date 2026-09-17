using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Deposit.Interest;

/// <summary>
/// Interest rate information for different account types and balances
/// </summary>
public class InterestRateInfo
{
    public AccountType AccountType { get; set; }
    public decimal MinimumBalance { get; set; }
    public decimal MaximumBalance { get; set; }
    public decimal InterestRate { get; set; }
}

