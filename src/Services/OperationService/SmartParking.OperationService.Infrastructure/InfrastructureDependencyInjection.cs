using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Infrastructure.Clients;
using SmartParking.OperationService.Infrastructure.Outbox;
using SmartParking.OperationService.Persistence;

namespace SmartParking.OperationService.Infrastructure;

public static class InfrastructureDependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddPersistenceServices(configuration);
        services.AddHttpContextAccessor();

        services.AddHttpClient<OperatorIdentityClient>(client =>
        {
            client.BaseAddress = new(configuration["Services:User"] ?? "http://localhost:5035/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<IOperatorIdentityClient>(provider =>
            provider.GetRequiredService<OperatorIdentityClient>());

        var serviceKey = configuration["Services:Key"]
            ?? throw new InvalidOperationException("Configure Services:Key.");
        services.AddHttpClient<ParkingGateSlotClient>(client =>
        {
            client.BaseAddress = new(configuration["Services:Facility"] ?? "http://localhost:5045/");
            client.Timeout = TimeSpan.FromSeconds(10);
            client.DefaultRequestHeaders.Add("X-Service-Key", serviceKey);
        });
        services.AddScoped<IOperatorGateSlotClaims>(provider =>
            provider.GetRequiredService<ParkingGateSlotClient>());

        services.AddHttpClient<CheckoutPaymentClient>(client =>
        {
            client.BaseAddress = new(configuration["Services:Payment"] ?? "http://localhost:5065/");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddScoped<ICheckoutPaymentClient>(provider =>
            provider.GetRequiredService<CheckoutPaymentClient>());

        AddOutboxPublisher(services, configuration);

        return services;
    }

    private static void AddOutboxPublisher(IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration["RabbitMq:ConnectionString"]
            ?? configuration.GetConnectionString("RabbitMq");
        var exchange = configuration["RabbitMq:OccupancyExchange"] ?? "smartpark.occupancy";

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton<IOutboxEventPublisher, DisabledOutboxEventPublisher>();
            return;
        }

        services.Configure<RabbitMqOutboxOptions>(options =>
        {
            options.ConnectionString = connectionString;
            options.Exchange = exchange;
        });
        services.AddSingleton<IOutboxEventPublisher, RabbitMqOutboxPublisher>();
        services.AddHostedService<OccupancyOutboxWorker>();
    }
}

internal sealed class DisabledOutboxEventPublisher(ILogger<DisabledOutboxEventPublisher> logger) : IOutboxEventPublisher
{
    private static bool _warned;

    public string Name => "disabled";

    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        if (!_warned)
        {
            _warned = true;
            logger.LogWarning("Occupancy outbox publisher disabled: RabbitMq:ConnectionString is not configured. Events remain unpublished.");
        }
        return Task.CompletedTask;
    }
}
