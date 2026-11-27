using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.PassPlans.Options;
using SmartParking.OperationService.Application.PassPlans.Ports;
using SmartParking.OperationService.Infrastructure.Clients;
using SmartParking.OperationService.Infrastructure.Outbox;
using SmartParking.OperationService.Infrastructure.PassPlans;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Infrastructure.Reservations;

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

        services.AddHttpClient("RealtimeUserAuthority", client =>
        {
            client.BaseAddress = new(configuration["Services:User"] ?? "http://localhost:5035/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddHttpClient("RealtimeFacilityAuthority", client =>
        {
            client.BaseAddress = new(configuration["Services:Facility"] ?? configuration["Services:Parking"] ?? "http://localhost:5045/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<IRealtimeRoleSubscriptionAuthorizer, RealtimeRoleSubscriptionAuthorizer>();

        services.AddHttpClient<OwnerIdentityClient>(client =>
        {
            client.BaseAddress = new(configuration["Services:User"] ?? "http://localhost:5035/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<IOwnerIdentityClient>(provider =>
            provider.GetRequiredService<OwnerIdentityClient>());

        var serviceKey = configuration["Services:Key"]
            ?? throw new InvalidOperationException("Configure Services:Key.");
        services.AddHttpClient<ReservationCommercialClient>(client =>
        {
            client.BaseAddress = new(configuration["Services:Payment"] ?? "http://localhost:5065/");
            client.Timeout = TimeSpan.FromSeconds(15);
        });
        services.AddScoped<IReservationCommercialOwner>(p => p.GetRequiredService<ReservationCommercialClient>());
        if (configuration.GetValue<bool>("ReservationIntegration:ReviewedSettlementEnabled"))
            services.AddScoped<IReservationQuoteOwner>(p => p.GetRequiredService<ReservationCommercialClient>());
        services.AddHttpClient<ReservationFacilityClient>(client =>
        {
            client.BaseAddress = new(configuration["Services:Facility"] ?? configuration["Services:Parking"] ?? "http://localhost:5045/");
            client.Timeout = TimeSpan.FromSeconds(10);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<IReservationFacilityOwner>(p => p.GetRequiredService<ReservationFacilityClient>());
        services.AddHttpClient<ParkingGateSlotClient>(client =>
        {
            client.BaseAddress = new(configuration["Services:Parking"]
                ?? configuration["Services:Facility"]
                ?? "http://localhost:5045/");
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

        services.AddScoped<Application.Policies.Ports.ISiteOwnershipValidator, Policies.SiteOwnershipValidator>();

        services.Configure<PassPlanDefaultsOptions>(configuration.GetSection(PassPlanDefaultsOptions.SectionName));
        services.AddHttpClient<PassPlanFacilityScopeAdapter>(client =>
        {
            client.BaseAddress = new(configuration["Services:Facility"] ?? configuration["Services:Parking"] ?? "http://localhost:5045/");
            client.Timeout = TimeSpan.FromSeconds(10);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<IPassPlanFacilityScopePort>(provider =>
            provider.GetRequiredService<PassPlanFacilityScopeAdapter>());

        services.AddHttpClient<PaymentPassTermsHttpAdapter>(client =>
        {
            client.BaseAddress = new(configuration["Services:Payment"] ?? "http://localhost:5065/");
            client.Timeout = TimeSpan.FromSeconds(10);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
        services.AddScoped<IPaymentPassTermsPort>(provider =>
            provider.GetRequiredService<PaymentPassTermsHttpAdapter>());

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
