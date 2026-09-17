using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Deposit.Withdrawal;

/// <summary>
/// Deposit certificate data transfer object
/// </summary>
public class DepositCertificateDto
{
    public Guid Id { get; set; }
    public Guid FixedDepositId { get; set; }
    public string CertificateNumber { get; set; } = string.Empty;
    public DepositCertificateStatus Status { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string DeliveryMethod { get; set; } = string.Empty;
    public string? DeliveryAddress { get; set; }
    public string? DeliveryReference { get; set; }
    public string? PdfFileName { get; set; }
    public bool HasPdf { get; set; }
}


