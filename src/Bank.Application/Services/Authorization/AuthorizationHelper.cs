using Bank.Application.Interfaces.Authorization;
using Microsoft.Extensions.Logging;

namespace Bank.Application.Services.Authorization;

/// <summary>
/// Centralized helper for authorization and ownership verification operations.
/// Eliminates duplicate authorization checks across controllers.
/// </summary>
public sealed class AuthorizationHelper : IAuthorizationHelper
{
    private readonly ILogger<AuthorizationHelper> _logger;

    public AuthorizationHelper(ILogger<AuthorizationHelper> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Verifies that a user owns a specific resource.
    /// Automatically handles admin access (admins can access any resource).
    /// </summary>
    public async Task<(bool IsAuthorized, string? ErrorMessage)> VerifyResourceOwnershipAsync<T>(
        Guid resourceId,
        Guid userId,
        Func<Guid, Task<T?>> resourceGetter,
        Func<T, Guid> getOwnerId,
        string? userRole = null,
        string resourceName = "Resource") where T : class
    {
        try
        {
            // Admins have unrestricted access
            if (IsAdmin(userRole))
            {
                _logger.LogInformation("Admin user {UserId} accessing {ResourceName} {ResourceId}", userId, resourceName, resourceId);
                return (true, null);
            }

            // Fetch the resource
            var resource = await resourceGetter(resourceId);
            if (resource == null)
            {
                var errorMessage = $"{resourceName} {resourceId} not found";
                _logger.LogWarning("Resource not found: {ResourceName} {ResourceId}", resourceName, resourceId);
                return (false, errorMessage);
            }

            // Verify ownership
            var ownerId = getOwnerId(resource);
            if (ownerId != userId)
            {
                var errorMessage = $"You can only access your own {resourceName.ToLower()}";
                _logger.LogWarning("Unauthorized access attempt: User {UserId} tried to access {ResourceName} {ResourceId} owned by {OwnerId}", 
                    userId, resourceName, resourceId, ownerId);
                return (false, errorMessage);
            }

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying ownership for {ResourceName} {ResourceId}", resourceName, resourceId);
            return (false, "An error occurred while verifying ownership");
        }
    }

    /// <summary>
    /// Checks if a user has any of the specified roles.
    /// </summary>
    public bool HasRole(string? userRole, params string[] roles)
    {
        if (string.IsNullOrEmpty(userRole))
            return false;

        return roles.Contains(userRole, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Checks if a user is an admin.
    /// </summary>
    public bool IsAdmin(string? userRole)
    {
        return HasRole(userRole, "Admin");
    }
}
