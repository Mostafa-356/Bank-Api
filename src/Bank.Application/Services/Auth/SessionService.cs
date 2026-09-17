using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Bank.Application.DTOs;
using Bank.Application.Interfaces;
using Bank.Application.Helpers;
using Bank.Application.Helpers.Shared;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Bank.Application.Services;

/// <summary>
/// Service for managing user sessions with timeout and concurrent session limits
/// </summary>
public sealed class SessionService : ISessionService
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IAuditEventPublisher _auditEventPublisher;
    private readonly ILogger<SessionService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IUnitOfWork _unitOfWork;

    // Configuration settings
    private readonly TimeSpan _defaultSessionTimeout;
    private readonly TimeSpan _defaultRefreshTokenTimeout;
    private readonly int _defaultMaxConcurrentSessions;
    private readonly TimeSpan _adminSessionTimeout;
    private readonly int _maxAdminConcurrentSessions;

    public SessionService(
        ISessionRepository sessionRepository,
        IUserRepository userRepository,
        IAuditEventPublisher auditEventPublisher,
        ILogger<SessionService> logger,
        IConfiguration configuration,
        IUnitOfWork unitOfWork)
    {
        _sessionRepository = sessionRepository;
        _userRepository = userRepository;
        _auditEventPublisher = auditEventPublisher;
        _logger = logger;
        _configuration = configuration;
        _unitOfWork = unitOfWork;

        // Load configuration settings
        _defaultSessionTimeout = TimeSpan.FromMinutes(int.Parse(_configuration["Security:Session:DefaultTimeoutMinutes"] ?? "30"));
        _defaultRefreshTokenTimeout = TimeSpan.FromDays(int.Parse(_configuration["Security:Session:RefreshTokenTimeoutDays"] ?? "7"));
        _defaultMaxConcurrentSessions = int.Parse(_configuration["Security:Session:MaxConcurrentSessions"] ?? "5");
        _adminSessionTimeout = TimeSpan.FromMinutes(int.Parse(_configuration["Security:Session:AdminTimeoutMinutes"] ?? "15"));
        _maxAdminConcurrentSessions = int.Parse(_configuration["Security:Session:MaxAdminConcurrentSessions"] ?? "2");
    }

    public async Task<SessionResult> CreateSessionAsync(Guid userId, string ipAddress, string userAgent, string? deviceFingerprint = null, bool isAdminSession = false)
    {
        try
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return new SessionResult { Success = false, ErrorMessage = "User not found" };
            }

            // Determine session settings based on admin status
            var sessionTimeout = isAdminSession ? _adminSessionTimeout : _defaultSessionTimeout;
            var maxConcurrentSessions = isAdminSession ? _maxAdminConcurrentSessions : _defaultMaxConcurrentSessions;

            // Enforce concurrent session limits
            await EnforceConcurrentSessionLimitsAsync(userId, maxConcurrentSessions);

            // Generate secure tokens
            var sessionToken = GenerateSecureToken();
            var refreshToken = GenerateSecureToken();

            // Create new session
            var session = new Session(
                userId,
                sessionToken,
                refreshToken,
                sessionTimeout,
                _defaultRefreshTokenTimeout,
                ipAddress,
                userAgent,
                deviceFingerprint,
                isAdminSession);

            await _sessionRepository.AddAsync(session);
            await _unitOfWork.SaveChangesAsync();

            // Get active session count
            var activeSessions = await _sessionRepository.GetActiveSessionsByUserIdAsync(userId);

            // Publish audit event
            await _auditEventPublisher.PublishSecurityEventAsync(
                userId,
                "SessionCreated",
                "Session",
                session.Id.ToString(),
                ipAddress,
                userAgent,
                JsonSerializer.Serialize(new { IsAdminSession = isAdminSession, DeviceFingerprint = deviceFingerprint }));

            _logger.LogInformation("Session created for user {UserId} from IP {IpAddress}. Admin session: {IsAdminSession}", 
                userId, ipAddress, isAdminSession);

            return new SessionResult
            { Success = true,
                SessionToken = sessionToken,
                RefreshToken = refreshToken,
                ExpiresAt = session.ExpiresAt,
                ActiveSessionCount = activeSessions.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating session for user {UserId}", userId);
            return new SessionResult { Success = false, ErrorMessage = "Failed to create session" };
        }
    }

    public async Task<Session?> GetSessionAsync(string sessionToken)
    {
        try
        {
            var session = await _sessionRepository.GetBySessionTokenAsync(sessionToken);
            
            if (session != null && session.IsValid())
            {
                await UpdateSessionActivityAsync(sessionToken);
                return session;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving session with token {SessionTokenMasked}", MaskToken(sessionToken));
            return null;
        }
    }

    public async Task UpdateSessionActivityAsync(string sessionToken)
    {
        try
        {
            var session = await _sessionRepository.GetBySessionTokenAsync(sessionToken);
            if (session != null && session.IsValid())
            {
                session.UpdateActivity();
                _sessionRepository.Update(session);
                await _unitOfWork.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating session activity for token {SessionTokenMasked}", MaskToken(sessionToken));
        }
    }

    public async Task TerminateSessionAsync(string sessionToken, string reason)
    {
        try
        {
            var session = await _sessionRepository.GetBySessionTokenAsync(sessionToken);
            if (session != null)
            {
                session.Terminate(reason);
                _sessionRepository.Update(session);
                await _unitOfWork.SaveChangesAsync();

                // Publish audit event
                await _auditEventPublisher.PublishSecurityEventAsync(
                    session.UserId,
                    "SessionTerminated",
                    "Session",
                    session.Id.ToString(),
                    session.IpAddress,
                    session.UserAgent,
                    JsonSerializer.Serialize(new { Reason = reason }));

                _logger.LogInformation("Session {SessionId} terminated for user {UserId}. Reason: {Reason}", 
                    session.Id, session.UserId, reason);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error terminating session with token {SessionTokenMasked}", MaskToken(sessionToken));
        }
    }

    public async Task TerminateAllUserSessionsAsync(Guid userId, string reason, string? excludeSessionToken = null)
    {
        try
        {
            var sessions = await _sessionRepository.GetActiveSessionsByUserIdAsync(userId);
            var terminatedCount = 0;

            foreach (var session in sessions)
            {
                if (excludeSessionToken != null && session.SessionToken == excludeSessionToken)
                    continue;

                session.Terminate(reason);
                _sessionRepository.Update(session);
                await _unitOfWork.SaveChangesAsync();
                terminatedCount++;
            }

            // Publish audit event
            await _auditEventPublisher.PublishSecurityEventAsync(
                userId,
                "AllSessionsTerminated",
                "User",
                userId.ToString(),
                additionalData: JsonSerializer.Serialize(new { Reason = reason, TerminatedCount = terminatedCount }));

            _logger.LogInformation("Terminated {Count} sessions for user {UserId}. Reason: {Reason}", 
                terminatedCount, userId, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error terminating all sessions for user {UserId}", userId);
        }
    }

    public async Task<List<Session>> GetUserActiveSessionsAsync(Guid userId)
    {
        try
        {
            return await _sessionRepository.GetActiveSessionsByUserIdAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active sessions for user {UserId}", userId);
            return new List<Session>();
        }
    }

    public async Task<SessionResult> RefreshSessionAsync(string refreshToken)
    {
        try
        {
            var session = await _sessionRepository.GetByRefreshTokenAsync(refreshToken);
            
            if (session == null || session.IsRefreshTokenExpired() || session.Status != SessionStatus.Active)
            {
                return new SessionResult { Success = false, ErrorMessage = "Invalid or expired refresh token" };
            }

            // Generate new tokens
            var newSessionToken = GenerateSecureToken();
            var newRefreshToken = GenerateSecureToken();

            // Determine session timeout based on admin status
            var sessionTimeout = session.IsAdminSession ? _adminSessionTimeout : _defaultSessionTimeout;

            // Refresh the session
            session.RefreshTokens(newSessionToken, newRefreshToken, sessionTimeout, _defaultRefreshTokenTimeout);
            _sessionRepository.Update(session);
            await _unitOfWork.SaveChangesAsync();

            // Publish audit event
            await _auditEventPublisher.PublishSecurityEventAsync(
                session.UserId,
                "SessionRefreshed",
                "Session",
                session.Id.ToString(),
                session.IpAddress,
                session.UserAgent);

            _logger.LogInformation("Session refreshed for user {UserId}", session.UserId);

            return new SessionResult
            { Success = true,
                SessionToken = newSessionToken,
                RefreshToken = newRefreshToken,
                ExpiresAt = session.ExpiresAt
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing session with refresh token {RefreshTokenMasked}", SecureLoggingService.SanitizeInput(MaskToken(refreshToken)));
            return new SessionResult { Success = false, ErrorMessage = "Failed to refresh session" };
        }
    }

    public async Task CleanupExpiredSessionsAsync()
    {
        try
        {
            var expiredSessions = await _sessionRepository.GetExpiredSessionsAsync();
            var cleanedUpCount = 0;

            foreach (var session in expiredSessions)
            {
                if (session.Status == SessionStatus.Active)
                {
                    session.Terminate("Expired");
                    _sessionRepository.Update(session);
                    await _unitOfWork.SaveChangesAsync();
                    cleanedUpCount++;
                }
            }

            _logger.LogInformation("Cleaned up {Count} expired sessions", cleanedUpCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up expired sessions");
        }
    }

    public async Task EnforceConcurrentSessionLimitsAsync(Guid userId, int maxConcurrentSessions)
    {
        try
        {
            var activeSessions = await _sessionRepository.GetActiveSessionsByUserIdAsync(userId);
            
            if (activeSessions.Count >= maxConcurrentSessions)
            {
                // Terminate oldest sessions to make room
                var sessionsToTerminate = activeSessions
                    .OrderBy(s => s.LastActivityAt)
                    .Take(activeSessions.Count - maxConcurrentSessions + 1)
                    .ToList();

                foreach (var session in sessionsToTerminate)
                {
                    session.Terminate("Concurrent session limit exceeded");
                    _sessionRepository.Update(session);
                }
                await _unitOfWork.SaveChangesAsync();

                _logger.LogInformation("Terminated {Count} sessions for user {UserId} due to concurrent session limit", 
                    sessionsToTerminate.Count, userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enforcing concurrent session limits for user {UserId}", userId);
        }
    }

    public async Task<bool> IsSessionValidAsync(string sessionToken)
    {
        try
        {
            var session = await _sessionRepository.GetBySessionTokenAsync(sessionToken);
            return session?.IsValid() ?? false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating session with token {SessionTokenMasked}", MaskToken(sessionToken));
            return false;
        }
    }

    public async Task<SessionStatistics> GetSessionStatisticsAsync()
    {
        try
        {
            var allSessions = await _sessionRepository.GetAllSessionsAsync();
            var activeSessions = allSessions.Where(s => s.Status == SessionStatus.Active).ToList();

            return new SessionStatistics
            {
                TotalActiveSessions = activeSessions.Count,
                TotalAdminSessions = activeSessions.Count(s => s.IsAdminSession),
                TotalUserSessions = activeSessions.Count(s => !s.IsAdminSession),
                SessionsByStatus = allSessions.GroupBy(s => s.Status.ToString())
                    .ToDictionary(g => g.Key, g => g.Count()),
                LastCleanupAt = DateTime.UtcNow // This would be tracked separately in production
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving session statistics");
            return new SessionStatistics();
        }
    }

    private static string GenerateSecureToken()
    {
        return Bank.Application.Helpers.Auth.AuthGeneratorHelper.GenerateSecureToken(32);
    }

    /// <summary>
    /// Masks a sensitive token for safe logging â€” shows only the first 4 and last 4 characters.
    /// Never log raw session or refresh tokens.
    /// </summary>
    private static string MaskToken(string? token)
    {
        if (string.IsNullOrEmpty(token)) return "[empty]";
        if (token.Length <= 8) return "[redacted]";
        return $"{token[..4]}...{token[^4..]}";
    }
}