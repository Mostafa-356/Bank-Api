using System.Security.Claims;
using Bank.Domain.Entities;

namespace Bank.Application.Interfaces.Security;

public interface ITokenService
{
    Task<string> GenerateAccessTokenAsync(User user);
    Task<string> GenerateRefreshTokenAsync();
    ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}
