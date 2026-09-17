using Bank.Application.Interfaces;
using Bank.Domain.Entities;
using Bank.Domain.Enums;
using Bank.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

/// <summary>
/// Service for managing audit logs with immutable storage and comprehensive querying capabilities.
/// Implements regulatory compliance requirements for audit trail maintenance.
/// </summary>
public class AuditLogService : IAuditLogService
{
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(IAuditLogRepository auditLogRepository, ILogger<AuditLogService> logger)
    {
        _auditLogRepository = auditLogRepository ?? throw new ArgumentNullException(nameof(auditLogRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AuditLog> LogUserActionAsync(
        Guid userId,
        string action,
        string entityType,
        string entityId,
        string? oldValues = null,
        string? newValues = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? sessionId = null,
        string? requestId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var auditLog = AuditLog.CreateUserAction(
                userId,
                action,
                entityType,
                entityId,
                oldValues,
                newValues,
                ipAddress,
                userAgent,
                sessionId,
                requestId);

            var savedAuditLog = await _auditLogRepository.AddAsync(auditLog, cancellationToken);

            _logger.LogInformation(
                "User action audit log created: UserId={UserId}, Action={Action}, EntityType={EntityType}, EntityId={EntityId}",
                userId, action, entityType, entityId);

            return savedAuditLog;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create user action audit log: UserId={UserId}, Action={Action}, EntityType={EntityType}, EntityId={EntityId}",
                userId, action, entityType, entityId);
            throw;
        }
    }

    public async Task<AuditLog> LogSystemEventAsync(
        string action,
        string entityType,
        string entityId,
        string? additionalData = null,
        string? requestId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var auditLog = AuditLog.CreateSystemEvent(
                action,
                entityType,
                entityId,
                additionalData,
                requestId);

            var savedAuditLog = await _auditLogRepository.AddAsync(auditLog, cancellationToken);

            _logger.LogInformation(
                "System event audit log created: Action={Action}, EntityType={EntityType}, EntityId={EntityId}",
                action, entityType, entityId);

            return savedAuditLog;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create system event audit log: Action={Action}, EntityType={EntityType}, EntityId={EntityId}",
                action, entityType, entityId);
            throw;
        }
    }

    public async Task<AuditLog> LogSecurityEventAsync(
        Guid? userId,
        string action,
        string entityType,
        string entityId,
        string? ipAddress = null,
        string? userAgent = null,
        string? additionalData = null,
        string? sessionId = null,
        string? requestId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var auditLog = AuditLog.CreateSecurityEvent(
                userId,
                action,
                entityType,
                entityId,
                ipAddress,
                userAgent,
                additionalData,
                sessionId,
                requestId);

            var savedAuditLog = await _auditLogRepository.AddAsync(auditLog, cancellationToken);

            _logger.LogWarning(
                "Security event audit log created: UserId={UserId}, Action={Action}, EntityType={EntityType}, EntityId={EntityId}, IpAddress={IpAddress}",
                userId, action, entityType, entityId, ipAddress);

            return savedAuditLog;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to create security event audit log: UserId={UserId}, Action={Action}, EntityType={EntityType}, EntityId={EntityId}",
                userId, action, entityType, entityId);
            throw;
        }
    }

    public async Task<IEnumerable<AuditLog>> GetUserAuditLogsAsync(
        Guid userId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return await _auditLogRepository.GetByUserIdAsync(
            userId, fromDate, toDate, pageNumber, pageSize, cancellationToken);
    }

    public async Task<IEnumerable<AuditLog>> GetEntityAuditLogsAsync(
        string entityType,
        string entityId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return await _auditLogRepository.GetByEntityAsync(
            entityType, entityId, fromDate, toDate, pageNumber, pageSize, cancellationToken);
    }

    public async Task<IEnumerable<AuditLog>> GetAuditLogsByEventTypeAsync(
        AuditEventType eventType,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return await _auditLogRepository.GetByEventTypeAsync(
            eventType, fromDate, toDate, pageNumber, pageSize, cancellationToken);
    }

    public async Task<IEnumerable<AuditLog>> GetAuditLogsByActionAsync(
        string action,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return await _auditLogRepository.GetByActionAsync(
            action, fromDate, toDate, pageNumber, pageSize, cancellationToken);
    }

    public async Task<IEnumerable<AuditLog>> GetAuditLogsByIpAddressAsync(
        string ipAddress,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return await _auditLogRepository.GetByIpAddressAsync(
            ipAddress, fromDate, toDate, pageNumber, pageSize, cancellationToken);
    }

    public async Task<Dictionary<string, int>> GetAuditLogStatisticsAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        return await _auditLogRepository.GetStatisticsAsync(fromDate, toDate, cancellationToken);
    }

    public async Task LogAsync(string action, string description, Guid? userId = null)
    {
        if (userId.HasValue)
        {
            await LogUserActionAsync(
                userId.Value,
                action,
                "General",
                Guid.NewGuid().ToString(),
                null,
                description);
        }
        else
        {
            await LogSystemEventAsync(
                action,
                "General",
                Guid.NewGuid().ToString(),
                description);
        }
    }

    public async Task LogActivityAsync(Guid userId, string action, string entityType, string entityId, string description)
    {
        await LogUserActionAsync(
            userId,
            action,
            entityType,
            entityId,
            null,
            description);
    }
}