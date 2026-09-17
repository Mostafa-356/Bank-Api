using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Statement.Search;

/// <summary>
/// Statement search criteria
/// </summary>
public class StatementSearchCriteria
{
    public Guid? AccountId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public StatementStatus? Status { get; set; }
    public StatementFormat? Format { get; set; }
    public bool? IsDelivered { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

