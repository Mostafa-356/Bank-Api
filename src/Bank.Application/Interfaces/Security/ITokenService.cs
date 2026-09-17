using System.Security.Claims;
using Bank.Domain.Entities;

namespace Bank.Application.Interfaces.Security;

/// <summary>
/// Canonical interface for JWT access token and refresh token operations.
/// Implemented once in Infrastructure (JwtTokenService). Do not create other implementations.
/// </summary>
public interface ITokenService
{
    /// <summary>Generates a signed JWT access token for the given user.</summary>
    Task<string> GenerateAccessTokenAsync(User user);

    /// <summary>Generates a signed JWT access token for the given user with explicit role claims.</summary>
    Task<string> GenerateAccessTokenAsync(User user, IList<string> roles);

    /// <summary>Generates a cryptographically random opaque refresh token.</summary>
    Task<string> GenerateRefreshTokenAsync();

    /// <summary>Extracts and validates a ClaimsPrincipal from an expired (but otherwise valid) JWT.</summary>
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
