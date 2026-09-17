using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Statement.Delivery;

/// <summary>
/// Request model for delivering statements
/// </summary>
public class DeliverStatementRequest
{
    public StatementDeliveryMethod DeliveryMethod { get; set; }
    public string DeliveryAddress { get; set; } = string.Empty;
}

