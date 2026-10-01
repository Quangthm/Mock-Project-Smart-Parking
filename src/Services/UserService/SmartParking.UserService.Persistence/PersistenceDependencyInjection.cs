using Microsoft.Extensions.DependencyInjection;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Persistence.Repositories;

namespace UserService.Persistence;

public static class PersistenceDependencyInjection
{
    public static IServiceCollection AddPersistenceServices(
        this IServiceCollection services)
    {
        services.AddScoped<IUserRepository, UserRepository>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}