using SmartParking.Contracts.ReservationPayments;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MassTransit;
using SmartParking.Contracts.Audit;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartParking.Contracts.AccountAccess;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Infrastructure.Messaging;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public static class ReservationMessaging
{
    public static IServiceCollection AddReservationMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddScoped<IReservationQuoteOwner, UnavailableReservationQuoteOwner>();
        var admissionEnabled = configuration.GetValue<bool>("ReservationAdmission:Enabled");
        var sagaEnabled = admissionEnabled && configuration.GetValue<bool>("ReservationAdmission:SagaConsumersEnabled");
        if (sagaEnabled)
        {
            if (string.IsNullOrWhiteSpace(configuration["ReservationAdmission:BrokerUser"]) ||
                string.IsNullOrWhiteSpace(configuration["ReservationAdmission:BrokerPassword"]) ||
                !configuration.GetValue<bool>("Messaging:OutboxDeliveryEnabled"))
                throw new InvalidOperationException("Saga endpoints require restricted credentials and durable outbox delivery.");
            services.AddScoped<AdmissionMessages>();
            services.AddScoped<IAdmissionMessages>(provider => provider.GetRequiredService<AdmissionMessages>());
            if (configuration.GetValue<bool>("ReservationIntegration:Enabled") &&
                configuration.GetValue<bool>("ReservationIntegration:ReviewedSettlementEnabled"))
                services.TryAddScoped<IQuoteAdmissionStarter>(provider => provider.GetRequiredService<AdmissionMessages>());
        }
        services.TryAddScoped<IQuoteAdmissionStarter, UnavailableQuoteAdmissionStarter>();
        var auditEnabled = configuration.GetValue<bool>("Messaging:AuditEnabled");
        var gateCheckoutEnabled = configuration.GetValue<bool>("Messaging:Enabled");
        if (auditEnabled) services.AddScoped<IOperationsAuditPublisher, OperationsAuditPublisher>();
        else services.AddScoped<IOperationsAuditPublisher, UnconfiguredOperationsAuditPublisher>();
        if (!admissionEnabled && !auditEnabled)
        {
            // MediatR handlers remain registered when routes are disabled. Keep DI valid and
            // fail closed even if an internal caller attempts dispatch without the HTTP gate.
            services.TryAddScoped<IAccountAccessRequester, DisabledAccountAccessRequester>();
            services.TryAddScoped<IReservationFactsPublisher, DisabledReservationFactsPublisher>();
            if (!gateCheckoutEnabled) return services;
        }
        else
        {
            services.AddScoped<IAccountAccessRequester, AccountAccessRequester>();
            services.AddScoped<IReservationFactsPublisher, ReservationFactsPublisher>();
        }
        var restrictedBroker = admissionEnabled || auditEnabled;
        var brokerUser = (restrictedBroker && !string.IsNullOrWhiteSpace(configuration["ReservationAdmission:BrokerUser"]))
            ? configuration["ReservationAdmission:BrokerUser"]
            : configuration["SMARTPARK_RABBITMQ_USER"] ?? "smartpark";
        var brokerPassword = (restrictedBroker && !string.IsNullOrWhiteSpace(configuration["ReservationAdmission:BrokerPassword"]))
            ? configuration["ReservationAdmission:BrokerPassword"]
            : configuration["SMARTPARK_RABBITMQ_PASSWORD"];
        if (string.IsNullOrWhiteSpace(brokerUser) || string.IsNullOrWhiteSpace(brokerPassword))
            throw new InvalidOperationException("Configure Operations broker credentials for enabled messaging.");
        services.AddMassTransit(registration =>
        {
            if (sagaEnabled)
            {
                registration.AddConsumer<AccountAccessDecisionConsumer>();
                registration.AddConsumer<QuoteEvaluationConsumer>();
                registration.AddConsumer<PlateDecisionConsumer>();
                registration.AddConsumer<SlotDecisionConsumer>();
                registration.AddSagaStateMachine<ReservationAdmissionMachine, Persistence.Entities.ReservationAdmissionState>()
                    .EntityFrameworkRepository(repository =>
                    {
                        repository.ConcurrencyMode = ConcurrencyMode.Pessimistic;
                        repository.ExistingDbContext<ReservationDbContext>();
                        repository.UsePostgres();
                    });
            }
            if (admissionEnabled)
            {
                registration.AddConsumer<ReservationProgressConsumer>();
                registration.AddConsumer<ReservationZeroReceiptIssuedConsumer>();
                registration.AddConsumer<ReservationCheckoutChangedConsumer>();
                registration.AddConsumer<ReservationPaymentSucceededConsumer>();
            }
            if (gateCheckoutEnabled)
            {
                registration.AddConsumer<PaymentSucceededConsumer>();
            }
            if (restrictedBroker) registration.AddEntityFrameworkOutbox<ReservationDbContext>(outbox =>
            {
                outbox.UsePostgres();
                outbox.QueryDelay = TimeSpan.FromSeconds(1);
                outbox.UseBusOutbox(bus =>
                {
                    if (!configuration.GetValue<bool>("Messaging:OutboxDeliveryEnabled")) bus.DisableDeliveryService();
                });
            });
            registration.UsingRabbitMq((context, bus) =>
            {
                bus.Host(configuration["SMARTPARK_RABBITMQ_HOST"] ?? "127.0.0.1",
                    configuration.GetValue<ushort?>("SMARTPARK_RABBITMQ_PORT") ?? 5672,
                    configuration["SMARTPARK_RABBITMQ_VHOST"] ?? "smartpark", host =>
                    {
                        host.Username(brokerUser);
                        host.Password(brokerPassword);
                    });
                bus.Message<PaymentSucceededV1>(message => message.SetEntityName("smartpark.payment.payment-succeeded.v1"));
                if (gateCheckoutEnabled)
                {
                    bus.ReceiveEndpoint("smartpark-operation-payment-succeeded-v1", endpoint =>
                    {
                        endpoint.UseMessageRetry(retry => retry.Intervals(
                            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)));
                        endpoint.ConfigureConsumer<PaymentSucceededConsumer>(context);
                    });
                }
                if (admissionEnabled)
                {
                    bus.ReceiveEndpoint("smartpark-operation-reservation-payment-succeeded-v1", endpoint =>
                    {
                        endpoint.UseMessageRetry(retry => retry.Intervals(
                            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)));
                        endpoint.UseEntityFrameworkOutbox<ReservationDbContext>(context);
                        endpoint.ConfigureConsumer<ReservationPaymentSucceededConsumer>(context);
                    });
                }
                bus.Message<OperationsGateAuditV1>(message => message.SetEntityName(OperationsAuditTopology.Exchange));
                bus.Message<ReservationConfirmedV1>(message => message.SetEntityName("smartpark.operations.reservation-confirmed.v1"));
                bus.Message<ReservationHoldChangedV1>(message => message.SetEntityName("smartpark.operations.reservation-hold-changed.v1"));
                if (admissionEnabled)
                {
                    bus.ReceiveEndpoint(ReservationProgressPublisher.Queue, endpoint =>
                    {
                        endpoint.ConfigureConsumeTopology = false;
                        endpoint.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(1)));
                        endpoint.ConfigureConsumer<ReservationProgressConsumer>(context);
                    });
                    bus.ReceiveEndpoint(ReservationPaymentEndpoints.ZeroReceipt, endpoint =>
                    {
                        endpoint.UseMessageRetry(retry => retry.Intervals(
                            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)));
                        endpoint.ConfigureConsumeTopology = false;
                        endpoint.UseEntityFrameworkOutbox<ReservationDbContext>(context);
                        endpoint.ConfigureConsumer<ReservationZeroReceiptIssuedConsumer>(context);
                    });
                    bus.ReceiveEndpoint(ReservationPaymentEndpoints.CheckoutChanged, endpoint =>
                    {
                        endpoint.ConfigureConsumeTopology = false;
                        endpoint.ConfigureConsumer<ReservationCheckoutChangedConsumer>(context);
                    });
                }
                bus.Message<ReservationAvailabilityChangedV1>(message => message.SetEntityName("smartpark.operations.reservation-availability-changed.v1"));
                bus.Message<FacilityStructureAppliedV1>(message => message.SetEntityName("smartpark.operations.facility-structure-applied.v1"));
                bus.Publish<FacilityStructureAppliedV1>(publish =>
                { publish.Durable = true; publish.AutoDelete = false; publish.ExchangeType = "fanout"; });
                bus.Publish<ReservationHoldChangedV1>(publish =>
                { publish.Durable = true; publish.AutoDelete = false; publish.ExchangeType = "fanout"; });
                bus.Publish<ReservationAvailabilityChangedV1>(publish =>
                { publish.Durable = true; publish.AutoDelete = false; publish.ExchangeType = "fanout"; });
                bus.Publish<ReservationConfirmedV1>(publish =>
                { publish.Durable = true; publish.AutoDelete = false; publish.ExchangeType = "fanout"; });
                bus.Publish<OperationsGateAuditV1>(publish =>
                {
                    publish.Durable = true;
                    publish.AutoDelete = false;
                    publish.ExchangeType = "fanout";
                });
                // Audit owner provisions/binds the durable queue before relay is enabled.
                if (sagaEnabled)
                {
                    bus.ReceiveEndpoint("operations-reservation-plate-results-v1", endpoint =>
                    {
                        endpoint.ConfigureConsumeTopology = false;
                        endpoint.UseEntityFrameworkOutbox<ReservationDbContext>(context);
                        endpoint.ConfigureConsumer<PlateDecisionConsumer>(context);
                    });
                    bus.ReceiveEndpoint("operations-reservation-slot-results-v1", endpoint =>
                    {
                        endpoint.ConfigureConsumeTopology = false;
                        endpoint.UseEntityFrameworkOutbox<ReservationDbContext>(context);
                        endpoint.ConfigureConsumer<SlotDecisionConsumer>(context);
                    });
                    bus.ReceiveEndpoint(AdmissionMessages.EvaluationQueue, endpoint =>
                    {
                        endpoint.ConfigureConsumeTopology = false;
                        endpoint.UseMessageRetry(r => r.Interval(3, TimeSpan.FromSeconds(1)));
                        endpoint.ConfigureConsumer<QuoteEvaluationConsumer>(context);
                    });
                    bus.ReceiveEndpoint(AdmissionMessages.SagaQueue, endpoint =>
                    {
                        endpoint.ConfigureConsumeTopology = false;
                        endpoint.UseMessageRetry(r => r.Interval(3, TimeSpan.FromMilliseconds(250)));
                        endpoint.UseEntityFrameworkOutbox<ReservationDbContext>(context);
                        endpoint.ConfigureSaga<Persistence.Entities.ReservationAdmissionState>(context);
                    });
                }
                if (sagaEnabled) bus.ReceiveEndpoint(AccountAccessEndpoints.Decisions, endpoint =>
                {
                    endpoint.UseMessageRetry(retry =>
                    {
                        retry.Handle<Npgsql.NpgsqlException>(error => error.IsTransient);
                        retry.Interval(1, TimeSpan.FromMilliseconds(250));
                    });
                    endpoint.UseEntityFrameworkOutbox<ReservationDbContext>(context);
                    endpoint.ConfigureConsumer<AccountAccessDecisionConsumer>(context);
                });
            });
        });
        services.Configure<MassTransitHostOptions>(options => options.WaitUntilStarted = false);
        if (sagaEnabled) services.AddHostedService<ReservationAuthorizationWorker>();
        if (admissionEnabled)
        {
            services.AddScoped<IReservationProgressPublisher, ReservationProgressPublisher>();
            services.AddHostedService<ReservationHoldExpiryWorker>();
        }
        return services;
    }
}
