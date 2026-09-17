using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Payment.Beneficiary;

/// <summary>
/// Beneficiary statistics
/// </summary>
public class BeneficiaryStatistics
{
    public int TotalBeneficiaries { get; set; }
    public int ActiveBeneficiaries { get; set; }
    public int VerifiedBeneficiaries { get; set; }
    public int PendingVerification { get; set; }
    public Dictionary<BeneficiaryCategory, int> BeneficiariesByCategory { get; set; } = new();
    public Dictionary<BeneficiaryType, int> BeneficiariesByType { get; set; } = new();
    public decimal TotalTransferAmount { get; set; }
    public int TotalTransfers { get; set; }
}

