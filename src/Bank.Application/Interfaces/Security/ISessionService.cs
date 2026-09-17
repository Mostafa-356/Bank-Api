using System;
using System.Threading.Tasks;

namespace Bank.Application.Interfaces.Security;

public interface ISessionService
{
    Task RevokeSessionAsync(Guid sessionId, Guid userId);
    Task RevokeAllSessionsForUserAsync(Guid userId);
}
