namespace Bank.Application.DTOs.Payment.Beneficiary;

/// <summary>
/// Beneficiary transfer history
/// </summary>
public class BeneficiaryTransferHistory
{
    public Guid BeneficiaryId { get; set; }
    public string BeneficiaryName { get; set; } = string.Empty;
    public List<TransferHistoryItem> Transfers { get; set; } = new();
    public decimal TotalAmount { get; set; }
    public int TotalCount { get; set; }
}

