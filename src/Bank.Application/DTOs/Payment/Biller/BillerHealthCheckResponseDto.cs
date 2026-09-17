namespace Bank.Application.DTOs.Payment.Biller;

public class BillerHealthCheckResponse
{
    public Guid BillerId { get; set; }
    public bool IsHealthy { get; set; }
    public DateTime LastChecked { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public Dictionary<string, object> HealthMetrics { get; set; } = new();
}

