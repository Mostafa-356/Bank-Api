namespace Bank.Application.DTOs.Payment.Beneficiary;

/// <summary>
/// Beneficiary search result
/// </summary>
public class BeneficiarySearchResult
{
    public List<BeneficiaryDto> Beneficiaries { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

