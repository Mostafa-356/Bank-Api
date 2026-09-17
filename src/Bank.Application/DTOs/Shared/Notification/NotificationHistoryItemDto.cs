using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Shared.Notification;

/// <summary>
/// Notification history item
/// </summary>
public class NotificationHistoryItem
{
    public string Id { get; set; } = string.Empty;
    public NotificationType Type { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationChannel Channel { get; set; }
    public NotificationStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? ErrorMessage { get; set; }
}

