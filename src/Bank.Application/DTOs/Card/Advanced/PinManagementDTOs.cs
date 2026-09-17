using System.ComponentModel.DataAnnotations;

namespace Bank.Application.DTOs.Card.Advanced;

/// <summary>
/// Request to set or change card PIN
/// </summary>
public class SetPinRequest
{
    [Required]
    public string CardId { get; set; } = string.Empty;
    
    [Required]
    [StringLength(4, MinimumLength = 4, ErrorMessage = "PIN must be exactly 4 digits")]
    [RegularExpression(@"^\d{4}$", ErrorMessage = "PIN must contain only digits")]
    public string NewPin { get; set; } = string.Empty;
    
    /// <summary>
    /// Current PIN (required for PIN change, not for initial PIN set)
    /// </summary>
    public string? CurrentPin { get; set; }
}

/// <summary>
/// Request to reset card PIN
/// </summary>
public class ResetPinRequest
{
    [Required]
    public string CardId { get; set; } = string.Empty;
    
    /// <summary>
    /// Verification method (SMS, Email, etc.)
    /// </summary>
    [Required]
    public string VerificationMethod { get; set; } = string.Empty;
    
    /// <summary>
    /// Verification code sent to customer
    /// </summary>
    [Required]
    public string VerificationCode { get; set; } = string.Empty;
    
    [Required]
    [StringLength(4, MinimumLength = 4, ErrorMessage = "PIN must be exactly 4 digits")]
    [RegularExpression(@"^\d{4}$", ErrorMessage = "PIN must contain only digits")]
    public string NewPin { get; set; } = string.Empty;
}

/// <summary>
/// Request to verify card PIN
/// </summary>
public class VerifyPinRequest
{
    [Required]
    public string CardId { get; set; } = string.Empty;
    
    [Required]
    [StringLength(4, MinimumLength = 4, ErrorMessage = "PIN must be exactly 4 digits")]
    [RegularExpression(@"^\d{4}$", ErrorMessage = "PIN must contain only digits")]
    public string Pin { get; set; } = string.Empty;
}

/// <summary>
/// Response for PIN operations
/// </summary>
public class PinOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsCardBlocked { get; set; }
    public int RemainingAttempts { get; set; }
}

/// <summary>
/// PIN verification result
/// </summary>
public class PinVerificationResult
{
    public bool IsValid { get; set; }
    public bool IsCardBlocked { get; set; }
    public int RemainingAttempts { get; set; }
    public string Message { get; set; } = string.Empty;
}

