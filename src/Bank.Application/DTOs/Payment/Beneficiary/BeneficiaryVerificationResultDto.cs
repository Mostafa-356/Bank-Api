using Bank.Application.DTOs.Common;

namespace Bank.Application.DTOs.Payment.Beneficiary;

/// <summary>
/// Result of beneficiary verification
/// </summary>
public class BeneficiaryVerificationResult : BaseResultDto
{
    public bool IsAccountValid { get; set; }
    public string? AccountHolderName { get; set; }
    public string? BankName { get; set; }
    public List<string> ValidationErrors { get; set; } = new();
}


