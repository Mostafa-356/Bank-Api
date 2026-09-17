using Bank.Application.Interfaces.Security;
using Bank.Infrastructure.Services.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Bank.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Register HTTP Context Accessor for CurrentUserAccessor
        services.AddHttpContextAccessor();

        // Register Security Canonical Services
        services.AddScoped<ICurrentUser, CurrentUserAccessor>();
        services.AddScoped<ITokenService, JwtTokenService>();

        // (Other infrastructure registrations would go here: DbContext, Repositories, etc.)
        
        return services;
    }
}
