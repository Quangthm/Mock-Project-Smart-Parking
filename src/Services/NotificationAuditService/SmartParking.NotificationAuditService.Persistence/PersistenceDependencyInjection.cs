using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Persistence.Context;
using SmartParking.NotificationAuditService.Persistence.Repositories;

namespace SmartParking.NotificationAuditService.Persistence;

public static class PersistenceDependencyInjection
{
    public static IServiceCollection AddPersistenceServices(
        this IServiceCollection services,
        IConfiguration? configuration = null)
    {
        services.AddDbContext<NotificationAuditDbContext>((serviceProvider, options) =>
        {
            var config = configuration ?? serviceProvider.GetService<IConfiguration>();
            var connectionString = config?.GetConnectionString("NotificationAudit")
                                   ?? config?.GetConnectionString("DefaultConnection");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                // Fallback matching port 5445 architecture baseline
                connectionString = "Host=localhost;Port=5445;Database=smartpark_notification;Username=postgres;Password=postgres";
            }

            options.UseNpgsql(connectionString);
        });

        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationTemplateRepository, NotificationTemplateRepository>();
        services.AddScoped<ICentralAuditProjectionRepository, CentralAuditProjectionRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
