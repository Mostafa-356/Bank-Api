using Bank.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Bank.Application.DTOs.Shared.Notification;

/// <summary>
/// Request to send a notification
/// </summary>
public class SendNotificationRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    public NotificationType Type { get; set; }
    
    [Required]
    public string Subject { get; set; } = string.Empty;
    
    [Required]
    public string Message { get; set; } = string.Empty;
    
    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;
    
    public Dictionary<string, object>? Data { get; set; }
    
    public NotificationPriority Priority { get; set; } = NotificationPriority.Normal;
    
    public DateTime? ScheduledAt { get; set; }
}

