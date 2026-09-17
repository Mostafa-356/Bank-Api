using Bank.Application.DTOs.Common;

namespace Bank.Application.DTOs.Payment.Beneficiary;

/// <summary>
/// Result of beneficiary operations
/// </summary>
public class BeneficiaryResult : BaseResultDto
{
    public BeneficiaryDto? Beneficiary { get; set; }
}


