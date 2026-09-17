namespace Bank.Application.DTOs.Account.Transfer;

/// <summary>
/// Transfer pre-validation result
/// </summary>
public class TransferPreValidationResult
{
    public bool IsValid { get; set; }
    public bool SufficientBalance { get; set; }
    public bool BeneficiaryEligible { get; set; }
    public bool WithinLimits { get; set; }
    public bool PassesFraudCheck { get; set; }
    public decimal AvailableBalance { get; set; }
    public TransferEligibilityResult? EligibilityResult { get; set; }
    public BeneficiaryLimitsResult? LimitsResult { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public string? RecommendedAction { get; set; }
    public DateTime ValidationDate { get; set; } = DateTime.UtcNow;
}

