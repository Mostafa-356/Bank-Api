using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Loan.Configuration;

/// <summary>
/// DTO for loan search and filtering
/// </summary>
public class LoanSearchRequest
{
    public Guid? CustomerId { get; set; }
    public LoanType? Type { get; set; }
    public LoanStatus? Status { get; set; }
    public decimal? MinAmount { get; set; }
    public decimal? MaxAmount { get; set; }
    public DateTime? ApplicationDateFrom { get; set; }
    public DateTime? ApplicationDateTo { get; set; }
    public bool? IsOverdue { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string SortBy { get; set; } = "ApplicationDate";
    public bool SortDescending { get; set; } = true;
}

