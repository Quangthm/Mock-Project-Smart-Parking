using MassTransit;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.NotificationAuditService.Application;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Infrastructure;
using SmartParking.NotificationAuditService.Persistence;
using SmartParking.NotificationAuditService.Persistence.Context;

namespace SmartParking.NotificationAuditService.Tests.Integration;

public class DependencyInjectionTests
{
    [Fact]
    public void ServiceProvider_ShouldResolveAllRequiredDependencies_WithoutExceptions()
    {
        var services = new ServiceCollection();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:NotificationAudit"] = "Host=localhost;Port=5445;Database=smartpark_notification;Username=postgres;Password=postgres",
                ["RabbitMQ:UseInMemory"] = "true"
            })
            .Build();

        services.AddLogging();
        services.AddApplicationServices();
        services.AddPersistenceServices(configuration);
        services.AddInfrastructureServices(configuration);

        var provider = services.BuildServiceProvider();

        // 1. MediatR
        var mediator = provider.GetService<IMediator>();
        Assert.NotNull(mediator);

        // 2. DbContext
        using var scope = provider.CreateScope();
        var dbContext = scope.ServiceProvider.GetService<NotificationAuditDbContext>();
        Assert.NotNull(dbContext);

        // 3. Repositories & UnitOfWork
        Assert.NotNull(scope.ServiceProvider.GetService<INotificationRepository>());
        Assert.NotNull(scope.ServiceProvider.GetService<INotificationTemplateRepository>());
        Assert.NotNull(scope.ServiceProvider.GetService<ICentralAuditProjectionRepository>());
        Assert.NotNull(scope.ServiceProvider.GetService<IUnitOfWork>());

        // 4. MassTransit Bus
        var bus = scope.ServiceProvider.GetService<IBusControl>();
        Assert.NotNull(bus);
    }
}
