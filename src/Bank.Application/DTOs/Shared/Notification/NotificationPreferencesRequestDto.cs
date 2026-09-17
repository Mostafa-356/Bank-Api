using Bank.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Bank.Application.DTOs.Shared.Notification;

/// <summary>
/// Notification preference settings
/// </summary>
public class NotificationPreferencesRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    public bool TransactionAlerts { get; set; } = true;
    
    public bool SecurityAlerts { get; set; } = true;
    
    public bool LowBalanceAlerts { get; set; } = true;
    
    public bool PaymentReminders { get; set; } = true;
    
    public bool MarketingNotifications { get; set; } = false;
    
    public decimal TransactionAlertThreshold { get; set; } = 0m;
    
    public decimal LowBalanceThreshold { get; set; } = 100m;
    
    public List<NotificationChannel> PreferredChannels { get; set; } = new();
    
    public string? PhoneNumber { get; set; }
    
    public string? Email { get; set; }
    
    public string Language { get; set; } = "en";
    
    public string TimeZone { get; set; } = "UTC";
}

