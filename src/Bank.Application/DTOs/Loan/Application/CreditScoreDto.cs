using Bank.Application.DTOs.Common;
using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Loan.Application;

/// <summary>
/// DTO for credit scoring result
/// </summary>
public class CreditScoreResult : BaseResultDto
{
    public int CreditScore { get; set; }
    public CreditScoreRange ScoreRange { get; set; }
    public string RiskAssessment { get; set; } = string.Empty;
    public decimal RecommendedInterestRate { get; set; }
    public decimal MaxLoanAmount { get; set; }
    public List<string> RiskFactors { get; set; } = new();
    public DateTime ScoringDate { get; set; }
}


