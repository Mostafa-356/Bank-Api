using System.ComponentModel.DataAnnotations;

namespace Bank.Application.DTOs.Shared.Notification;

/// <summary>
/// Transaction alert request
/// </summary>
public class TransactionAlertRequest
{
    [Required]
    public string TransactionId { get; set; } = string.Empty;
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    public decimal Amount { get; set; }
    
    [Required]
    public string Description { get; set; } = string.Empty;
    
    public string? MerchantName { get; set; }
    
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    
    public bool IsInternational { get; set; }
    
    public bool IsSuspicious { get; set; }
}

