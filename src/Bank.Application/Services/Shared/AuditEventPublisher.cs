using Bank.Application.Interfaces;
using Bank.Domain.Events;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services;

/// <summary>
/// Service for publishing audit events using domain events.
/// Provides methods for publishing different types of audit events that will be handled by event handlers.
/// </summary>
public class AuditEventPublisher : IAuditEventPublisher
{
    private readonly IMediator _mediator;
    private readonly ILogger<AuditEventPublisher> _logger;

    public AuditEventPublisher(IMediator mediator, ILogger<AuditEventPublisher> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task PublishEntityCreatedAsync(
        Guid? userId,
        string entityType,
        string entityId,
        string newValues,
        string? ipAddress = null,
        string? userAgent = null,
        string? sessionId = null,
        string? requestId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var domainEvent = new EntityCreatedEvent(
                userId,
                entityType,
                entityId,
                newValues,
                ipAddress,
                userAgent,
                sessionId,
                requestId);

            await _mediator.Publish(domainEvent, cancellationToken);

            _logger.LogDebug(
                "Entity created event published: EntityType={EntityType}, EntityId={EntityId}, UserId={UserId}",
                entityType, entityId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish entity created event: EntityType={EntityType}, EntityId={EntityId}, UserId={UserId}",
                entityType, entityId, userId);
            // Don't rethrow - event publishing should not break business operations
        }
    }

    public async Task PublishEntityUpdatedAsync(
        Guid? userId,
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
            var domainEvent = new EntityUpdatedEvent(
                userId,
                entityType,
                entityId,
                oldValues,
                newValues,
                ipAddress,
                userAgent,
                sessionId,
                requestId);

            await _mediator.Publish(domainEvent, cancellationToken);

            _logger.LogDebug(
                "Entity updated event published: EntityType={EntityType}, EntityId={EntityId}, UserId={UserId}",
                entityType, entityId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish entity updated event: EntityType={EntityType}, EntityId={EntityId}, UserId={UserId}",
                entityType, entityId, userId);
            // Don't rethrow - event publishing should not break business operations
        }
    }

    public async Task PublishEntityDeletedAsync(
        Guid? userId,
        string entityType,
        string entityId,
        string? oldValues = null,
        string? ipAddress = null,
        string? userAgent = null,
        string? sessionId = null,
        string? requestId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var domainEvent = new EntityDeletedEvent(
                userId,
                entityType,
                entityId,
                oldValues,
                ipAddress,
                userAgent,
                sessionId,
                requestId);

            await _mediator.Publish(domainEvent, cancellationToken);

            _logger.LogDebug(
                "Entity deleted event published: EntityType={EntityType}, EntityId={EntityId}, UserId={UserId}",
                entityType, entityId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish entity deleted event: EntityType={EntityType}, EntityId={EntityId}, UserId={UserId}",
                entityType, entityId, userId);
            // Don't rethrow - event publishing should not break business operations
        }
    }

    public async Task PublishSecurityEventAsync(
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
            var domainEvent = new SecurityEvent(
                userId,
                action,
                entityType,
                entityId,
                ipAddress,
                userAgent,
                additionalData,
                sessionId,
                requestId);

            await _mediator.Publish(domainEvent, cancellationToken);

            _logger.LogWarning(
                "Security event published: Action={Action}, EntityType={EntityType}, EntityId={EntityId}, UserId={UserId}, IpAddress={IpAddress}",
                action, entityType, entityId, userId, ipAddress);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to publish security event: Action={Action}, EntityType={EntityType}, EntityId={EntityId}, UserId={UserId}",
                action, entityType, entityId, userId);
            // Don't rethrow - event publishing should not break business operations
        }
    }
}