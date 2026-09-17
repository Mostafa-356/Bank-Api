using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Statement.Analytics;

/// <summary>
/// Statement template configuration
/// </summary>
public class StatementTemplate
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public StatementFormat Format { get; set; }
    public string? LogoPath { get; set; }
    public string? HeaderTemplate { get; set; }
    public string? FooterTemplate { get; set; }
    public Dictionary<string, object> CustomFields { get; set; } = new();
    public bool IncludeBankBranding { get; set; } = true;
    public bool IncludeRegulatoryDisclosures { get; set; } = true;
    public string? CssStyles { get; set; }
}

