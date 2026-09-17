using Bank.Application.DTOs.Statement.Core;

namespace Bank.Application.DTOs.Statement.Search;

/// <summary>
/// Statement search result
/// </summary>
public class StatementSearchResult
{
    public List<StatementDto> Statements { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

