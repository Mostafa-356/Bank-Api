using Bank.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Bank.Application.DTOs.Shared.Notification;

/// <summary>
/// Bulk notification request
/// </summary>
public class BulkNotificationRequest
{
    [Required]
    public List<string> UserIds { get; set; } = new();
    
    [Required]
    public NotificationType Type { get; set; }
    
    [Required]
    public string Subject { get; set; } = string.Empty;
    
    [Required]
    public string Message { get; set; } = string.Empty;
    
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;
    
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    
    public DateTime? ScheduledAt { get; set; }
    
    public Dictionary<string, object>? Data { get; set; }
}

