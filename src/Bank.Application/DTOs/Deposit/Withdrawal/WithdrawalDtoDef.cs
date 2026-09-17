namespace Bank.Application.DTOs.Deposit.Withdrawal;
using Bank.Domain.Enums;

public class WithdrawalDto
{
    public Guid DepositId { get; set; }
    public decimal Amount { get; set; }
    public DateTime WithdrawalDate { get; set; }
    public WithdrawalType WithdrawalType { get; set; }
    public string? Reason { get; set; }
    public decimal? PenaltyAmount { get; set; }
    public decimal? PenaltyPercentage { get; set; }
}
