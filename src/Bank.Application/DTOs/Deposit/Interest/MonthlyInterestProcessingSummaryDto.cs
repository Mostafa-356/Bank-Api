namespace Bank.Application.DTOs.Deposit.Interest;

/// <summary>
/// Monthly interest processing summary
/// </summary>
public class MonthlyInterestProcessingSummary
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int TotalAccountsProcessed { get; set; }
    public int SuccessfulApplications { get; set; }
    public int FailedApplications { get; set; }
    public decimal TotalInterestApplied { get; set; }
    public DateTime ProcessingDate { get; set; }
}

