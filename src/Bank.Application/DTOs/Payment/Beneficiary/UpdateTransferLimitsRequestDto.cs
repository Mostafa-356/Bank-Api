namespace Bank.Application.DTOs.Payment.Beneficiary;

/// <summary>
/// Request to update transfer limits
/// </summary>
public class UpdateTransferLimitsRequest
{
    public decimal? DailyLimit { get; set; }
    public decimal? MonthlyLimit { get; set; }
    public decimal? SingleLimit { get; set; }
}

