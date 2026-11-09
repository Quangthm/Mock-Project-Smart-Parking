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
        if (!admissionEnabled && !auditEnabled) return services;
        var brokerUser = configuration["ReservationAdmission:BrokerUser"];
        var brokerPassword = configuration["ReservationAdmission:BrokerPassword"];
        if (string.IsNullOrWhiteSpace(brokerUser) || string.IsNullOrWhiteSpace(brokerPassword))
            throw new InvalidOperationException("Configure Operations restricted broker credentials for durable messaging.");
        services.AddScoped<IAccountAccessRequester, AccountAccessRequester>();
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
        if (admissionEnabled) services.AddHostedService<ReservationAuthorizationWorker>();
        return services;
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
                    await requestScope.ServiceProvider.GetRequiredService<IMediator>()
                        .Send(new AdvanceReservationAuthorizationCommand(id), stoppingToken);
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
