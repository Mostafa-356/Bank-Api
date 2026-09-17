using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Card.Core;

/// <summary>
/// Account summary for card details
/// </summary>
public class AccountSummaryDto
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public AccountType Type { get; set; }
    public decimal Balance { get; set; }
}


