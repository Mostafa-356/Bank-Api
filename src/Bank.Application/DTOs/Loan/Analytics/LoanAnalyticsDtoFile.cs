using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Loan.Analytics;

/// <summary>
/// DTO for loan analytics and reporting
/// </summary>
public class LoanAnalyticsDto
{
    public int TotalLoans { get; set; }
    public decimal TotalLoanAmount { get; set; }
    public decimal TotalOutstandingBalance { get; set; }
    public decimal AverageInterestRate { get; set; }
    public int AverageTermMonths { get; set; }
    public Dictionary<LoanType, int> LoansByType { get; set; } = new();
    public Dictionary<LoanStatus, int> LoansByStatus { get; set; } = new();
    public Dictionary<CreditScoreRange, int> LoansByCreditScore { get; set; } = new();
    public decimal DelinquencyRate { get; set; }
    public decimal DefaultRate { get; set; }
    public DateTime ReportDate { get; set; }
}

