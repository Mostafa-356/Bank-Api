using Bank.Application.DTOs;
using Bank.Application.DTOs.Shared.Notification;
using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Bank.Application.Services;

/// <summary>
/// Service for managing notifications
/// </summary>
public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IUnitOfWork unitOfWork,
        ILogger<NotificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<NotificationResponse> SendNotificationAsync(SendNotificationRequest request)
    {
        try
        {
            if (!Guid.TryParse(request.UserId, out var userGuid))
            {
                return new NotificationResponse
                { Success = false,
                    Message = "Invalid user ID"
                };
            }

            // Check user preferences
            var preferences = await GetUserPreferencesAsync(userGuid);
            if (preferences != null && !preferences.IsNotificationTypeEnabled(request.Type))
            {
                return new NotificationResponse
                { Success = false,
                    Message = "Notification type disabled by user preferences"
                };
            }

            // Check quiet hours for non-critical notifications
            if (preferences != null && 
                preferences.IsInQuietHours(DateTime.UtcNow) && 
                !preferences.ShouldSendDuringQuietHours(request.Priority))
            {
                // Schedule for later
                request.ScheduledAt = DateTime.UtcNow.Date.AddDays(1).Add(preferences.QuietHoursEnd ?? TimeSpan.FromHours(8));
            }

            var notification = new Notification
            {
                UserId = userGuid,
                Type = request.Type,
                Subject = request.Subject,
                Message = request.Message,
                Channel = request.Channel,
                Priority = request.Priority,
                ScheduledAt = request.ScheduledAt,
                Data = request.Data != null ? JsonSerializer.Serialize(request.Data) : null,
                Language = preferences?.Language ?? "en"
            };

            await _unitOfWork.Repository<Notification>().AddAsync(notification);
            await _unitOfWork.SaveChangesAsync();

            // Send immediately if not scheduled
            if (!request.ScheduledAt.HasValue || request.ScheduledAt.Value <= DateTime.UtcNow)
            {
                await SendNotificationNow(notification);
            }

            _logger.LogInformation("Notification {NotificationId} created for user {UserId}", 
                notification.Id, request.UserId);

            return new NotificationResponse
            {
                NotificationId = notification.Id.ToString(), Success = true,
                Message = "Notification sent successfully",
                Status = notification.Status,
                SentAt = notification.SentAt ?? DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification to user {UserId}", request.UserId);
            
            return new NotificationResponse
            { Success = false,
                Message = "An error occurred while sending notification"
            };
        }
    }

    public async Task<NotificationResponse> SendTransactionAlertAsync(TransactionAlertRequest request)
    {
        try
        {
            var preferences = await GetUserPreferencesAsync(Guid.Parse(request.UserId));
            if (preferences != null && !preferences.ShouldAlertForTransaction(request.Amount))
            {
                return new NotificationResponse
                { Success = false,
                    Message = "Transaction amount below alert threshold"
                };
            }

            var message = $"Transaction Alert: {request.Description} - ${request.Amount:F2}";
            if (!string.IsNullOrEmpty(request.MerchantName))
            {
                message += $" at {request.MerchantName}";
            }

            if (request.IsInternational)
            {
                message += " (International)";
            }

            if (request.IsSuspicious)
            {
                message += " - SUSPICIOUS ACTIVITY DETECTED";
            }

            var notificationRequest = new SendNotificationRequest
            {
                UserId = request.UserId,
                Type = NotificationType.TransactionAlert,
                Subject = "Transaction Alert",
                Message = message,
                Channel = NotificationChannel.InApp,
                Priority = request.IsSuspicious ? NotificationPriority.Critical : NotificationPriority.Normal,
                Data = new Dictionary<string, object>
                {
                    ["transactionId"] = request.TransactionId,
                    ["amount"] = request.Amount,
                    ["merchantName"] = request.MerchantName ?? "",
                    ["isInternational"] = request.IsInternational,
                    ["isSuspicious"] = request.IsSuspicious
                }
            };

            return await SendNotificationAsync(notificationRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending transaction alert for user {UserId}", request.UserId);
            
            return new NotificationResponse
            { Success = false,
                Message = "An error occurred while sending transaction alert"
            };
        }
    }

    public async Task<NotificationResponse> SendSecurityAlertAsync(SecurityAlertRequest request)
    {
        var notificationRequest = new SendNotificationRequest
        {
            UserId = request.UserId,
            Type = NotificationType.SecurityAlert,
            Subject = $"Security Alert: {request.AlertType}",
            Message = request.Description,
            Channel = NotificationChannel.InApp,
            Priority = NotificationPriority.High,
            Data = new Dictionary<string, object>
            {
                ["alertType"] = request.AlertType.ToString(),
                ["ipAddress"] = request.IpAddress ?? "",
                ["deviceInfo"] = request.DeviceInfo ?? "",
                ["location"] = request.Location ?? "",
                ["timestamp"] = request.Timestamp
            }
        };

        return await SendNotificationAsync(notificationRequest);
    }

    public async Task<List<NotificationResponse>> SendBulkNotificationAsync(BulkNotificationRequest request)
    {
        var responses = new List<NotificationResponse>();

        foreach (var userId in request.UserIds)
        {
            var individualRequest = new SendNotificationRequest
            {
                UserId = userId,
                Type = request.Type,
                Subject = request.Subject,
                Message = request.Message,
                Channel = request.Channel,
                Priority = request.Priority,
                ScheduledAt = request.ScheduledAt,
                Data = request.Data
            };

            var response = await SendNotificationAsync(individualRequest);
            responses.Add(response);
        }

        return responses;
    }

    public async Task<List<NotificationHistoryItem>> GetNotificationHistoryAsync(string userId, int page = 1, int pageSize = 20)
    {
        if (!Guid.TryParse(userId, out var userGuid))
        {
            return new List<NotificationHistoryItem>();
        }

        var notifications = await _unitOfWork.Repository<Notification>().Query()
            .Where(n => n.UserId == userGuid)
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationHistoryItem
            {
                Id = n.Id.ToString(),
                Type = n.Type,
                Subject = n.Subject,
                Message = n.Message,
                Channel = n.Channel,
                Status = n.Status,
                CreatedAt = n.CreatedAt,
                SentAt = n.SentAt,
                ReadAt = n.ReadAt,
                ErrorMessage = n.ErrorMessage
            })
            .ToListAsync();

        return notifications;
    }

    public async Task<bool> MarkAsReadAsync(string notificationId, string userId)
    {
        if (!Guid.TryParse(notificationId, out var notificationGuid) || 
            !Guid.TryParse(userId, out var userGuid))
        {
            return false;
        }

        var notification = await _unitOfWork.Repository<Notification>().Query()
            .FirstOrDefaultAsync(n => n.Id == notificationGuid && n.UserId == userGuid);

        if (notification == null)
        {
            return false;
        }

        notification.MarkAsRead();
        _unitOfWork.Repository<Notification>().Update(notification);
        await _unitOfWork.SaveChangesAsync();

        return true;
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var userGuid))
        {
            return 0;
        }

        return await _unitOfWork.Repository<Notification>().Query()
            .CountAsync(n => n.UserId == userGuid && n.Status != NotificationStatus.Read);
    }

    public async Task<bool> UpdatePreferencesAsync(NotificationPreferencesRequest request)
    {
        try
        {
            if (!Guid.TryParse(request.UserId, out var userGuid))
            {
                return false;
            }

            var preferences = await _unitOfWork.Repository<NotificationPreference>().Query()
                .FirstOrDefaultAsync(p => p.UserId == userGuid);

            if (preferences == null)
            {
                preferences = new NotificationPreference
                {
                    UserId = userGuid
                };
                await _unitOfWork.Repository<NotificationPreference>().AddAsync(preferences);
            }

            preferences.TransactionAlerts = request.TransactionAlerts;
            preferences.SecurityAlerts = request.SecurityAlerts;
            preferences.LowBalanceAlerts = request.LowBalanceAlerts;
            preferences.PaymentReminders = request.PaymentReminders;
            preferences.MarketingNotifications = request.MarketingNotifications;
            preferences.TransactionAlertThreshold = request.TransactionAlertThreshold;
            preferences.LowBalanceThreshold = request.LowBalanceThreshold;
            preferences.PhoneNumber = request.PhoneNumber;
            preferences.Email = request.Email;
            preferences.Language = request.Language;
            preferences.TimeZone = request.TimeZone;

            preferences.SetPreferredChannels(request.PreferredChannels);

            _unitOfWork.Repository<NotificationPreference>().Update(preferences);
            await _unitOfWork.SaveChangesAsync();

            _logger.LogInformation("Notification preferences updated for user {UserId}", SecureLoggingService.SanitizeInput(request.UserId));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating notification preferences for user {UserId}", SecureLoggingService.SanitizeInput(request.UserId));
            return false;
        }
    }

    public async Task<NotificationPreferencesRequest?> GetPreferencesAsync(string userId)
    {
        if (!Guid.TryParse(userId, out var userGuid))
        {
            return null;
        }

        var preferences = await _unitOfWork.Repository<NotificationPreference>().Query()
            .FirstOrDefaultAsync(p => p.UserId == userGuid);

        if (preferences == null)
        {
            return null;
        }

        return new NotificationPreferencesRequest
        {
            UserId = userId,
            TransactionAlerts = preferences.TransactionAlerts,
            SecurityAlerts = preferences.SecurityAlerts,
            LowBalanceAlerts = preferences.LowBalanceAlerts,
            PaymentReminders = preferences.PaymentReminders,
            MarketingNotifications = preferences.MarketingNotifications,
            TransactionAlertThreshold = preferences.TransactionAlertThreshold,
            LowBalanceThreshold = preferences.LowBalanceThreshold,
            PreferredChannels = preferences.GetPreferredChannels(),
            PhoneNumber = preferences.PhoneNumber,
            Email = preferences.Email,
            Language = preferences.Language,
            TimeZone = preferences.TimeZone
        };
    }

    public async Task<NotificationResponse> SendLowBalanceAlertAsync(string userId, string accountId, decimal currentBalance, decimal threshold)
    {
        var notificationRequest = new SendNotificationRequest
        {
            UserId = userId,
            Type = NotificationType.LowBalance,
            Subject = "Low Balance Alert",
            Message = $"Your account balance is ${currentBalance:F2}, which is below your alert threshold of ${threshold:F2}.",
            Channel = NotificationChannel.InApp,
            Priority = NotificationPriority.Normal,
            Data = new Dictionary<string, object>
            {
                ["accountId"] = accountId,
                ["currentBalance"] = currentBalance,
                ["threshold"] = threshold
            }
        };

        return await SendNotificationAsync(notificationRequest);
    }

    public async Task<NotificationResponse> SendCardAlertAsync(string userId, string cardId, string alertType, string message)
    {
        var notificationRequest = new SendNotificationRequest
        {
            UserId = userId,
            Type = NotificationType.CardAlert,
            Subject = $"Card Alert: {alertType}",
            Message = message,
            Channel = NotificationChannel.InApp,
            Priority = NotificationPriority.Normal,
            Data = new Dictionary<string, object>
            {
                ["cardId"] = cardId,
                ["alertType"] = alertType
            }
        };

        return await SendNotificationAsync(notificationRequest);
    }

    public async Task<NotificationResponse> SendPaymentReminderAsync(string userId, string paymentType, decimal amount, DateTime dueDate)
    {
        var notificationRequest = new SendNotificationRequest
        {
            UserId = userId,
            Type = NotificationType.PaymentReminder,
            Subject = "Payment Reminder",
            Message = $"Your {paymentType} payment of ${amount:F2} is due on {dueDate:yyyy-MM-dd}.",
            Channel = NotificationChannel.InApp,
            Priority = NotificationPriority.Normal,
            Data = new Dictionary<string, object>
            {
                ["paymentType"] = paymentType,
                ["amount"] = amount,
                ["dueDate"] = dueDate
            }
        };

        return await SendNotificationAsync(notificationRequest);
    }

    public async Task SendSystemNotificationAsync(Guid userId, string title, string message, string type)
    {
        var request = new SendNotificationRequest
        {
            UserId = userId.ToString(),
            Subject = title,
            Message = message,
            Type = Enum.Parse<NotificationType>(type),
            Channel = NotificationChannel.InApp,
            Priority = NotificationPriority.High
        };

        await SendNotificationAsync(request);
    }

    public async Task ProcessScheduledNotificationsAsync()
    {
        var scheduledNotifications = await _unitOfWork.Repository<Notification>().Query()
            .Where(n => n.Status == NotificationStatus.Pending && 
                       n.ScheduledAt.HasValue && 
                       n.ScheduledAt.Value <= DateTime.UtcNow)
            .ToListAsync();

        foreach (var notification in scheduledNotifications)
        {
            await SendNotificationNow(notification);
        }

        if (scheduledNotifications.Any())
        {
            await _unitOfWork.SaveChangesAsync();
        }
    }

    public async Task RetryFailedNotificationsAsync()
    {
        var failedNotifications = await _unitOfWork.Repository<Notification>().Query()
            .Where(n => n.Status == NotificationStatus.Failed && n.RetryCount < n.MaxRetries)
            .ToListAsync();

        foreach (var notification in failedNotifications.Where(n => n.CanRetry()))
        {
            await SendNotificationNow(notification);
        }

        if (failedNotifications.Any())
        {
            await _unitOfWork.SaveChangesAsync();
        }
    }

    private async Task<NotificationPreference?> GetUserPreferencesAsync(Guid userId)
    {
        return await _unitOfWork.Repository<NotificationPreference>().Query()
            .FirstOrDefaultAsync(p => p.UserId == userId);
    }

    private async Task SendNotificationNow(Notification notification)
    {
        try
        {
            // Simulate sending notification based on channel
            // In a real implementation, this would integrate with actual providers
            switch (notification.Channel)
            {
                case NotificationChannel.Email:
                    await SendEmailNotification(notification);
                    break;
                case NotificationChannel.SMS:
                    await SendSmsNotification(notification);
                    break;
                case NotificationChannel.Push:
                    await SendPushNotification(notification);
                    break;
                case NotificationChannel.InApp:
                default:
                    // In-app notifications are just stored in database
                    notification.MarkAsSent();
                    notification.MarkAsDelivered();
                    break;
            }

            _logger.LogInformation("Notification {NotificationId} sent via {Channel}", 
                notification.Id, notification.Channel);
        }
        catch (Exception ex)
        {
            notification.MarkAsFailed($"Failed to send via {notification.Channel}: {ex.Message}");
            _logger.LogError(ex, "Failed to send notification {NotificationId} via {Channel}", 
                notification.Id, notification.Channel);
        }
    }

    private async Task SendEmailNotification(Notification notification)
    {
        // Simulate email sending
        await Task.Delay(100);
        notification.MarkAsSent($"email_{Guid.NewGuid()}");
        notification.MarkAsDelivered();
    }

    private async Task SendSmsNotification(Notification notification)
    {
        // Simulate SMS sending
        await Task.Delay(100);
        notification.MarkAsSent($"sms_{Guid.NewGuid()}");
        notification.MarkAsDelivered();
    }

    private async Task SendPushNotification(Notification notification)
    {
        // Simulate push notification sending
        await Task.Delay(100);
        notification.MarkAsSent($"push_{Guid.NewGuid()}");
        notification.MarkAsDelivered();
    }
}