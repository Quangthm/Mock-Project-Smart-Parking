using Microsoft.Extensions.DependencyInjection;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Persistence.Repositories;
using UserService.Application.Common.Interfaces.Services;

namespace UserService.Persistence;

public static class PersistenceDependencyInjection
{
    public static IServiceCollection AddPersistenceServices(
        this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAuthSessionStore, InMemoryAuthSessionStore>();

        return services;
    }
}
