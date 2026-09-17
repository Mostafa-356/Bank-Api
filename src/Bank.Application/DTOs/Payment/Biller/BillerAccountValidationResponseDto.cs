namespace Bank.Application.DTOs.Payment.Biller;

public class BillerAccountValidationResponse
{
    public bool IsValid { get; set; }
    public string AccountStatus { get; set; } = string.Empty;
    public string BillerName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, string> ValidationDetails { get; set; } = new();
}

