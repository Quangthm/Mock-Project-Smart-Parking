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

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class AccountAccessRequester(ISendEndpointProvider endpoints) : IAccountAccessRequester
{
    public async Task RequestAsync(AccountAccessRequestV1 request, CancellationToken ct)
    {
        var endpoint = await endpoints.GetSendEndpoint(new Uri($"queue:{AccountAccessEndpoints.Requests}"));
        await endpoint.Send(request, context =>
        {
            context.MessageId = request.AttemptId;
            context.CorrelationId = request.WorkflowId;
        }, ct);
    }
}

public sealed class DisabledAccountAccessRequester : IAccountAccessRequester
{
    public Task RequestAsync(AccountAccessRequestV1 request, CancellationToken ct) =>
        throw new ReservationGateException(503, "RESERVATION_ADMISSION_DISABLED", "Reservation admission is not enabled.");
}

public sealed class DisabledReservationFactsPublisher : IReservationFactsPublisher
{
    public Task AvailabilityChangedAsync(ReservationAvailabilityChangedV1 fact, CancellationToken ct) =>
        throw new ReservationGateException(503, "RESERVATION_MESSAGING_UNAVAILABLE", "Durable reservation messaging is not enabled.");
    public Task FacilityAppliedAsync(FacilityStructureAppliedV1 fact, CancellationToken ct) =>
        throw new ReservationGateException(503, "RESERVATION_MESSAGING_UNAVAILABLE", "Durable reservation messaging is not enabled.");
    public Task HoldChangedAsync(ReservationHoldChangedV1 fact, CancellationToken ct) =>
        throw new ReservationGateException(503, "RESERVATION_MESSAGING_UNAVAILABLE", "Durable reservation messaging is not enabled.");
    public Task ConfirmedAsync(ReservationConfirmedV1 fact, CancellationToken ct) =>
        throw new ReservationGateException(503, "RESERVATION_MESSAGING_UNAVAILABLE", "Durable reservation messaging is not enabled.");
}

public sealed class AccountAccessDecisionConsumer(IMediator mediator) : IConsumer<AccountAccessDecisionV1>
{
    public Task Consume(ConsumeContext<AccountAccessDecisionV1> context) =>
        mediator.Send(new RecordAccountAccessDecisionCommand(context.Message), context.CancellationToken);
}

public static class ReservationMessaging
{
    public static IServiceCollection AddReservationMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        var admissionEnabled = configuration.GetValue<bool>("ReservationAdmission:Enabled");
        var auditEnabled = configuration.GetValue<bool>("Messaging:AuditEnabled");
        if (auditEnabled) services.AddScoped<IOperationsAuditPublisher, OperationsAuditPublisher>();
        else services.AddScoped<IOperationsAuditPublisher, UnconfiguredOperationsAuditPublisher>();
        if (!admissionEnabled && !auditEnabled)
        {
            // MediatR handlers remain registered when routes are disabled. Keep DI valid and
            // fail closed even if an internal caller attempts dispatch without the HTTP gate.
            services.AddScoped<IAccountAccessRequester, DisabledAccountAccessRequester>();
            services.AddScoped<IReservationFactsPublisher, DisabledReservationFactsPublisher>();
            return services;
        }
        var brokerUser = configuration["ReservationAdmission:BrokerUser"];
        var brokerPassword = configuration["ReservationAdmission:BrokerPassword"];
        if (string.IsNullOrWhiteSpace(brokerUser) || string.IsNullOrWhiteSpace(brokerPassword))
            throw new InvalidOperationException("Configure Operations restricted broker credentials for durable messaging.");
        services.AddScoped<IAccountAccessRequester, AccountAccessRequester>();
        services.AddScoped<IReservationFactsPublisher, ReservationFactsPublisher>();
        services.AddMassTransit(registration =>
        {
            if (admissionEnabled) registration.AddConsumer<AccountAccessDecisionConsumer>();
            registration.AddEntityFrameworkOutbox<ReservationDbContext>(outbox =>
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
                bus.Message<OperationsGateAuditV1>(message => message.SetEntityName(OperationsAuditTopology.Exchange));
                bus.Message<ReservationConfirmedV1>(message => message.SetEntityName("smartpark.operations.reservation-confirmed.v1"));
                bus.Message<ReservationHoldChangedV1>(message => message.SetEntityName("smartpark.operations.reservation-hold-changed.v1"));
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
                if (admissionEnabled) bus.ReceiveEndpoint(AccountAccessEndpoints.Decisions, endpoint =>
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
        if (admissionEnabled)
        {
            services.AddHostedService<ReservationAuthorizationWorker>();
            services.AddHostedService<ReservationFacilityDependencyWorker>();
        }
        return services;
    }
}

// This queue is independent of receipt terminal state: completed/expired bookings can still
// owe a durable Facility completion or abort acknowledgement after a remote outage.
public sealed class ReservationFacilityDependencyWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<ReservationFacilityDependencyWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var queryScope = scopes.CreateScope();
                var due = await queryScope.ServiceProvider.GetRequiredService<IReservationFacilityDependencyRepository>()
                    .DueAsync(clock.GetUtcNow(), 100, stoppingToken);
                foreach (var token in due)
                {
                    try
                    {
                        using var scope = scopes.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<IMediator>()
                            .Send(new AdvanceFacilityDependencyCommand(token), stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception error)
                    {
                        logger.LogWarning("Facility dependency retained ({FailureType}).", error.GetType().Name);
                        try
                        {
                            using var retryScope = scopes.CreateScope();
                            await retryScope.ServiceProvider.GetRequiredService<IMediator>()
                                .Send(new ScheduleFacilityDependencyRetryCommand(token), stoppingToken);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                        catch (Exception retryError)
                        { logger.LogWarning("Facility dependency scheduling retained ({FailureType}).", retryError.GetType().Name); }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error)
            { logger.LogWarning("Facility dependency sweep retained ({FailureType}).", error.GetType().Name); }
        }
    }
}

public sealed class ReservationAuthorizationWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<ReservationAuthorizationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var due = await scope.ServiceProvider.GetRequiredService<IReservationRequestRepository>().DueAsync(clock.GetUtcNow(), stoppingToken);
                foreach (var id in due)
                {
                    using var requestScope = scopes.CreateScope();
                    try
                    {
                        var mediator = requestScope.ServiceProvider.GetRequiredService<IMediator>();
                        await mediator.Send(new AdvanceReservationAuthorizationCommand(id), stoppingToken);
                        await mediator.Send(new ProgressReservationCommand(id), stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception error)
                    {
                        logger.LogWarning("Reservation work retained ({FailureType}).", error.GetType().Name);
                        using var recoveryScope = scopes.CreateScope();
                        try
                        {
                            await recoveryScope.ServiceProvider.GetRequiredService<IMediator>()
                                .Send(new RecordReservationWorkFailureCommand(id), stoppingToken);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                        catch (Exception recoveryError)
                        { logger.LogWarning("Reservation recovery scheduling retained ({FailureType}).", recoveryError.GetType().Name); }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error)
            {
                // No raw SQL/connection/provider values in this background failure log.
                logger.LogWarning("Reservation authorization sweep failed ({FailureType}); pending state is retained.", error.GetType().Name);
            }
        }
    }
}
