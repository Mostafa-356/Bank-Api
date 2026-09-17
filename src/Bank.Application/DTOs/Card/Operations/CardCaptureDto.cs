using System.ComponentModel.DataAnnotations;

namespace Bank.Application.DTOs.Card.Operations;

/// <summary>
/// Request for capturing authorized transaction
/// </summary>
public class CardCaptureRequest
{
    [Required]
    public string AuthorizationCode { get; set; } = string.Empty;
    
    [Required]
    public decimal CaptureAmount { get; set; }
    
    public string? Reference { get; set; }
}

/// <summary>
/// Result of transaction capture
/// </summary>
public class CardCaptureResult
{
    public bool Success { get; set; }
    public string CaptureId { get; set; } = string.Empty;
    public decimal CapturedAmount { get; set; }
    public DateTime CaptureDate { get; set; }
    public string? ErrorMessage { get; set; }
}


