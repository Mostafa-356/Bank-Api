using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Loan.Analytics;

/// <summary>
/// DTO for loan origination trends
/// </summary>
public class LoanOriginationTrend
{
    public DateTime Month { get; set; }
    public int LoansOriginated { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AverageAmount { get; set; }
    public decimal AverageInterestRate { get; set; }
    public Dictionary<LoanType, int> OriginationsByType { get; set; } = new();
}

