using System.ComponentModel.DataAnnotations;

namespace Bank.Application.DTOs.Card.Operations;

/// <summary>
/// Request for voiding transaction
/// </summary>
public class CardVoidRequest
{
    [Required]
    public string AuthorizationCode { get; set; } = string.Empty;
    
    public string? Reason { get; set; }
}

/// <summary>
/// Result of transaction void
/// </summary>
public class CardVoidResult
{
    public bool Success { get; set; }
    public string VoidId { get; set; } = string.Empty;
    public DateTime VoidDate { get; set; }
    public string? ErrorMessage { get; set; }
}


