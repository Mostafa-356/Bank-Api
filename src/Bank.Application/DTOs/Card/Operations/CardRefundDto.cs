using System.ComponentModel.DataAnnotations;

namespace Bank.Application.DTOs.Card.Operations;

/// <summary>
/// Request for transaction refund
/// </summary>
public class CardRefundRequest
{
    [Required]
    public string TransactionId { get; set; } = string.Empty;
    
    [Required]
    public decimal RefundAmount { get; set; }
    
    public string? Reason { get; set; }
}

/// <summary>
/// Result of transaction refund
/// </summary>
public class CardRefundResult
{
    public bool Success { get; set; }
    public string RefundId { get; set; } = string.Empty;
    public decimal RefundedAmount { get; set; }
    public DateTime RefundDate { get; set; }
    public string? ErrorMessage { get; set; }
}


