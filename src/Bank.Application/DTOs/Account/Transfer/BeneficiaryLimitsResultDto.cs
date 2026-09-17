namespace Bank.Application.DTOs.Account.Transfer;

/// <summary>
/// Beneficiary limits result
/// </summary>
public class BeneficiaryLimitsResult
{
    public Guid BeneficiaryId { get; set; }
    public decimal? DailyLimit { get; set; }
    public decimal? MonthlyLimit { get; set; }
    public decimal? SingleTransferLimit { get; set; }
    public decimal DailyUsed { get; set; }
    public decimal MonthlyUsed { get; set; }
    public decimal? RemainingDaily { get; set; }
    public decimal? RemainingMonthly { get; set; }
    public DateTime CalculationDate { get; set; } = DateTime.UtcNow;
}

