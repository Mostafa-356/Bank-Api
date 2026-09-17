using Bank.Domain.Enums;

namespace Bank.Application.DTOs.Auth.Session;

public class SessionResult
{
    public bool Success { get; set; }
    public string? SessionToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? ErrorMessage { get; set; }
    public int ActiveSessionCount { get; set; }
}

public class SessionStatistics
{
    public int TotalActiveSessions { get; set; }
    public int TotalAdminSessions { get; set; }
    public int TotalUserSessions { get; set; }
    public int ExpiredSessionsCleanedUp { get; set; }
    public DateTime LastCleanupAt { get; set; }
    public Dictionary<string, int> SessionsByStatus { get; set; } = new();
}

public class SessionInfo
{
    public Guid Id { get; set; }
    public string SessionToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public SessionStatus Status { get; set; }
    public string IpAddress { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public DateTime LastActivityAt { get; set; }
    public bool IsAdminSession { get; set; }
    public DateTime CreatedAt { get; set; }
}
/// <summary>
/// Request model for refreshing session tokens
/// </summary>
public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

