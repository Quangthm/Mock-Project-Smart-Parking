using SmartParking.Contracts.ReservationPayments;
using MassTransit;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QuestPDF.Infrastructure;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.Common.Interfaces.Services;
using SmartParking.PaymentService.Application.Integrations;
using SmartParking.PaymentService.Infrastructure.Adapters;
using SmartParking.PaymentService.Infrastructure.Adapters.Outbound;
using SmartParking.PaymentService.Infrastructure.Messaging;
using SmartParking.PaymentService.Infrastructure.Options;
using SmartParking.PaymentService.Infrastructure.Services;

namespace SmartParking.PaymentService.Infrastructure;

public static class InfrastructureDependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureOutbox = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;
        QuestPDF.Settings.ThrowOnMissingFontFamilies = false;

        services.AddSingleton<IPdfInvoiceGenerator, QuestPdfInvoiceGenerator>();

        var reservationServiceBaseUrl = configuration["Services:OperationService:BaseUrl"] ?? "http://localhost:5002/";
        var userServiceBaseUrl = configuration["Services:User"] ?? "http://localhost:5035/";
        var facilityServiceBaseUrl = configuration["Services:Parking"]
            ?? configuration["Services:Facility"]
            ?? "http://localhost:5045/";
        var internalServiceKey = configuration["Services:Key"];

        services.Configure<PayOsOptions>(configuration.GetSection(PayOsOptions.SectionName));
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));
        services.AddScoped<IPaymentSucceededPublisher, PaymentSucceededPublisher>();

        // Thread-safe, mode-isolated payment integration settings. Persistence of accepted
        // changes/history is best-effort through the persistence layer (policy_revisions);
        // data protection encrypts stored credentials and a missing DB never breaks startup.
        //
        // The application discriminator is pinned so encrypted credentials remain decryptable
        // across container recreation (the default content-root-derived name can change).
        // When DataProtection:KeysDirectory is configured (expected to be a persistent mounted
        // volume), keys are persisted there instead of the ephemeral container filesystem.
        var dataProtection = services.AddDataProtection()
            .SetApplicationName("SmartParking.PaymentService");

        var dataProtectionKeysDirectory = configuration["DataProtection:KeysDirectory"];
        if (!string.IsNullOrWhiteSpace(dataProtectionKeysDirectory))
        {
            Directory.CreateDirectory(dataProtectionKeysDirectory);
            dataProtection.PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysDirectory));
        }

        services.AddSingleton<PaymentIntegrationSettingsStore>(sp => new PaymentIntegrationSettingsStore(
            sp.GetRequiredService<IOptions<PayOsOptions>>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IDataProtectionProvider>(),
            sp.GetService<ILogger<PaymentIntegrationSettingsStore>>()));
        services.AddSingleton<IPaymentIntegrationSettings>(
            sp => sp.GetRequiredService<PaymentIntegrationSettingsStore>());
        services.AddScoped<ICheckoutSigningBindings, CheckoutSigningBindings>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentActor, CurrentActorAdapter>();

        services.AddHttpClient("UserServiceClient", client =>
        {
            client.BaseAddress = new Uri(userServiceBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        services.AddHttpClient("FacilityServiceClient", client =>
        {
            client.BaseAddress = new Uri(facilityServiceBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

        services.AddScoped<IOwnerSiteAccessPort>(provider =>
        {
            var factory = provider.GetRequiredService<IHttpClientFactory>();
            var httpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();
            return new OwnerSiteAccessHttpAdapter(
                factory.CreateClient("UserServiceClient"),
                factory.CreateClient("FacilityServiceClient"),
                httpContextAccessor,
                internalServiceKey);
        });

        services.AddHttpClient<IReservationPricingSnapshotPort, ReservationSnapshotHttpAdapter>(client =>
        {
            client.BaseAddress = new Uri(reservationServiceBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(3);
        });

        services.AddHttpClient<IPaymentGatewayPort, PayOsAdapter>((serviceProvider, client) =>
        {
            var options = configuration.GetSection(PayOsOptions.SectionName).Get<PayOsOptions>() ?? new PayOsOptions();
            var baseUrl = string.IsNullOrWhiteSpace(options.BaseUrl) ? "https://api-merchant.payos.vn" : options.BaseUrl;
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(10);
        })
        // Prevent cross-origin redirects from carrying credentials to an unapproved destination.
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false
        });

        var useInMemory = configuration.GetValue<bool>("RabbitMQ:UseInMemory");

        services.AddScoped<SmartParking.PaymentService.Application.Reservations.IReservationFinancialNotifications, ReservationFinancialNotifications>();
        services.AddMassTransit(x =>
        {
            if (configuration.GetValue<bool>("ReservationIntegration:ReviewedSettlementEnabled"))
            {
                x.AddConsumer<EnsureReservationOrderConsumer>().Endpoint(e => e.Name = ReservationPaymentEndpoints.EnsureOrder);
                x.AddConsumer<ReservationDispositionConsumer>().Endpoint(e => e.Name = ReservationPaymentEndpoints.Disposition);
            }
            configureOutbox?.Invoke(x);

            if (useInMemory)
            {
                x.UsingInMemory((context, cfg) =>
                {
                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    cfg.Message<SmartParking.PaymentService.Domain.Events.PaymentSucceededV1>(message =>
                        message.SetEntityName("smartpark.payment.payment-succeeded.v1"));
                    var options = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>()
                                  ?? new RabbitMqOptions();

                    cfg.Host(options.Host, options.Port, options.VirtualHost, h =>
                    {
                        h.Username(options.Username);
                        h.Password(options.Password);
                    });

                    cfg.ConfigureEndpoints(context);
                });
            }
        });

        services.Configure<MassTransitHostOptions>(opt =>
        {
            opt.WaitUntilStarted = false;
            opt.StopTimeout = TimeSpan.FromSeconds(5);
        });

        return services;
    }
}
