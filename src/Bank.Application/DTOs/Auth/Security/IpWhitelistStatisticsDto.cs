using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Auth.Security;

public class IpWhitelistStatistics
{
    public int TotalActiveEntries { get; set; }
    public int PendingApprovals { get; set; }
    public int ExpiredEntries { get; set; }
    public Dictionary<IpWhitelistType, int> EntriesByType { get; set; } = new();
    public DateTime LastCleanupAt { get; set; }
}

