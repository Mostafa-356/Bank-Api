using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Shared.Notification;

/// <summary>
/// Notification response
/// </summary>
public class NotificationResponse
{
    public string NotificationId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public NotificationStatus Status { get; set; }
    public DateTime SentAt { get; set; }
    public List<string> FailedChannels { get; set; } = new();
}

