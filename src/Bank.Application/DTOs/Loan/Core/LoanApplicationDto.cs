using Bank.Application.DTOs.Common;
using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Loan.Core;

/// <summary>
/// DTO for loan application request
/// </summary>
public class LoanApplicationRequest
{
    public LoanType Type { get; set; }
    public decimal RequestedAmount { get; set; }
    public int TermInMonths { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public decimal MonthlyIncome { get; set; }
    public decimal ExistingDebtAmount { get; set; }
    public string EmploymentStatus { get; set; } = string.Empty;
    public int EmploymentYears { get; set; }
    public string? CollateralDescription { get; set; }
    public decimal? CollateralValue { get; set; }
}

/// <summary>
/// DTO for loan application result
/// </summary>
public class LoanApplicationResult : BaseResultDto
{
    public string LoanNumber { get; set; } = string.Empty;
    public Guid LoanId { get; set; }
    public LoanStatus Status { get; set; }
    public List<string> RequiredDocuments { get; set; } = new();
    public DateTime ApplicationDate { get; set; }
}


