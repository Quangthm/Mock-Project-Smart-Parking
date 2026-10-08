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
            options.UseNpgsql(configuration.GetConnectionString("User") ?? configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Configure ConnectionStrings:User for the service-owned User database.")));

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddTransient<DataSeeder>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IAuthSessionStore, PostgresAuthSessionStore>();
        services.AddScoped<IDriverRegistrationService, DriverRegistrationService>();
        services.AddScoped<DriverRegistrationService>();
        services.AddScoped<IOwnerRegistrationService, OwnerRegistrationService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IOperatorProvisioningService, OperatorProvisioningService>();
        services.AddScoped<AccountWorkflowService>();
        services.AddScoped<IAccountWorkflows>(sp=>sp.GetRequiredService<AccountWorkflowService>());
        services.AddScoped<IMfaPolicy>(sp=>sp.GetRequiredService<AccountWorkflowService>());
        services.AddScoped<VehicleRegistrationService>();
        services.AddScoped<IVehicleRegistration>(sp=>sp.GetRequiredService<VehicleRegistrationService>());

        return services;
    }
}
