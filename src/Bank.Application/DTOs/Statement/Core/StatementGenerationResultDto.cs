using Bank.Application.DTOs.Common;
using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Statement.Core;

/// <summary>
/// Statement generation result
/// </summary>
public class StatementGenerationResult : BaseResultDto
{
    public Guid? StatementId { get; set; }
    public string? StatementNumber { get; set; }
    public string? FileName { get; set; }
    public string? FilePath { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? DownloadUrl { get; set; }
    public DateTime? GeneratedDate { get; set; }
    public List<string> Warnings { get; set; } = new();
}


