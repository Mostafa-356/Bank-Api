using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Deposit.Maturity;

/// <summary>
/// Maturity notice data transfer object
/// </summary>
public class MaturityNoticeDto
{
    public Guid Id { get; set; }
    public Guid FixedDepositId { get; set; }
    public string NoticeNumber { get; set; } = string.Empty;
    public MaturityNoticeType NoticeType { get; set; }
    public DateTime NoticeDate { get; set; }
    public DateTime MaturityDate { get; set; }
    public NotificationStatus Status { get; set; }
    
    public string Subject { get; set; } = string.Empty;
    public NotificationChannel DeliveryChannel { get; set; }
    public string? DeliveryAddress { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public int DeliveryAttempts { get; set; }
    
    public DateTime? CustomerResponseDate { get; set; }
    public MaturityAction? CustomerChoice { get; set; }
    public string? CustomerInstructions { get; set; }
    public bool ConsentReceived { get; set; }
}


