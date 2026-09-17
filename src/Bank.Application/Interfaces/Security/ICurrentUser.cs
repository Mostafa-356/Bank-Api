using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace Bank.Application.Interfaces.Security;

/// <summary>
/// Abstracts the current authenticated user's details, decoupling the Application layer from HttpContext.
/// </summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
    string? Email { get; }
    bool IsAuthenticated { get; }
    IReadOnlyList<string> Roles { get; }
    IEnumerable<Claim> Claims { get; }
    bool IsInRole(string role);
}
