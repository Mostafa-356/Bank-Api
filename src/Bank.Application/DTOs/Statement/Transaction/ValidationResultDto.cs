namespace Bank.Application.DTOs.Statement.Transaction;

/// <summary>
/// Validation result model
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}

