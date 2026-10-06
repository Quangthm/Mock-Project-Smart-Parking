using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Persistence.Repositories;
using UserService.Application.Common.Interfaces.Services;

namespace UserService.Persistence;

public static class PersistenceDependencyInjection
{
    public static IServiceCollection AddPersistenceServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddTransient<DataSeeder>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IAuthSessionStore, PostgresAuthSessionStore>();
        services.AddScoped<IDriverRegistrationService, DriverRegistrationService>();
        services.AddScoped<IOwnerRegistrationService, OwnerRegistrationService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IOperatorProvisioningService, OperatorProvisioningService>();

        return services;
    }
}
