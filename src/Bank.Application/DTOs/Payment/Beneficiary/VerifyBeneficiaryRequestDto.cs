namespace Bank.Application.DTOs.Payment.Beneficiary;

/// <summary>
/// Request for beneficiary verification
/// </summary>
public class VerifyBeneficiaryRequest
{
    public Guid BeneficiaryId { get; set; }
    public bool ForceVerification { get; set; } = false;
}

