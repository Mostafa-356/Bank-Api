using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Statement.Delivery;

/// <summary>
/// Statement delivery status
/// </summary>
public class StatementDeliveryStatus
{
    public Guid StatementId { get; set; }
    public bool IsDelivered { get; set; }
    public DateTime? DeliveredDate { get; set; }
    public string? DeliveryReference { get; set; }
    public StatementDeliveryMethod DeliveryMethod { get; set; }
    public string? DeliveryAddress { get; set; }
    public List<string> DeliveryAttempts { get; set; } = new();
    public string? LastError { get; set; }
}

