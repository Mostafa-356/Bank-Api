using Bank.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Bank.Application.DTOs.Shared.Notification;

/// <summary>
/// Security alert request
/// </summary>
public class SecurityAlertRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    [Required]
    public SecurityAlertType AlertType { get; set; }
    
    [Required]
    public string Description { get; set; } = string.Empty;
    
    public string? IpAddress { get; set; }
    
    public string? DeviceInfo { get; set; }
    
    public string? Location { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

