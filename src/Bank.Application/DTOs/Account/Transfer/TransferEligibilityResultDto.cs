namespace Bank.Application.DTOs.Account.Transfer;

/// <summary>
/// Transfer eligibility result
/// </summary>
public class TransferEligibilityResult
{
    public bool IsEligible { get; set; }
    public bool BeneficiaryActive { get; set; }
    public bool BeneficiaryVerified { get; set; }
    public bool WithinTransferLimits { get; set; }
    public bool WithinDailyLimits { get; set; }
    public bool WithinMonthlyLimits { get; set; }
    public decimal? RemainingDailyLimit { get; set; }
    public decimal? RemainingMonthlyLimit { get; set; }
    public List<string> EligibilityIssues { get; set; } = new();
    public string? RecommendedAction { get; set; }
}

