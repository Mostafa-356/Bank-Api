using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Bank.Application.Interfaces.Security;
using Bank.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Bank.Infrastructure.Services.Security;

/// <summary>
/// Canonical JWT implementation. Reads from the unified "Jwt:" configuration section.
/// Throws InvalidOperationException if the signing key is not configured — no hardcoded fallback.
/// </summary>
public class JwtTokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <inheritdoc />
    public Task<string> GenerateAccessTokenAsync(User user)
        => GenerateAccessTokenAsync(user, Array.Empty<string>());

    /// <inheritdoc />
    public Task<string> GenerateAccessTokenAsync(User user, IList<string> roles)
    {
        var jwtSection = _configuration.GetSection("Jwt");

        var rawKey = jwtSection["Key"];
        if (string.IsNullOrWhiteSpace(rawKey))
            throw new InvalidOperationException(
                "JWT signing key is not configured. Set the 'Jwt:Key' configuration value (min 32 bytes).");

        var keyBytes = Encoding.UTF8.GetBytes(rawKey);
        if (keyBytes.Length < 32)
            throw new InvalidOperationException(
                "JWT signing key must be at least 32 bytes (256 bits). Update 'Jwt:Key'.");

        var expiryMinutes = int.TryParse(jwtSection["ExpiryMinutes"], out var mins) ? mins : 60;

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Name, user.UserName ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            // NameIdentifier mirrors Sub so ClaimTypes.NameIdentifier lookups work in controllers
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var signingKey = new SymmetricSecurityKey(keyBytes);
        var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSection["Issuer"],
            audience: jwtSection["Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: creds
        );

        return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
    }

    /// <inheritdoc />
    public Task<string> GenerateRefreshTokenAsync()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Task.FromResult(Convert.ToBase64String(randomNumber));
    }

    /// <inheritdoc />
    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var rawKey = _configuration["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(rawKey))
            throw new InvalidOperationException("JWT signing key is not configured.");

        var validationParameters = new TokenValidationParameters
        {
            ValidateAudience = false,
            ValidateIssuer = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(rawKey)),
            ValidateLifetime = false // Intentional: caller passes expired tokens for refresh flows
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(token, validationParameters, out var securityToken);

        if (securityToken is not JwtSecurityToken jwtToken ||
            !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityTokenException("Invalid token algorithm.");
        }

        return principal;
    }
}
