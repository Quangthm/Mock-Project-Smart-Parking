using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using SmartParking.OperationService.API;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Infrastructure.Reservations;
using SmartParking.OperationService.Persistence;

namespace SmartParking.OperationService.Tests;

[Collection("Reservation transport")]
public sealed class ReservationQuartzDurabilityTests : ICurrentActor
{
    public Guid? UserId { get; } = Guid.NewGuid();
    public Guid? AccountId { get; } = Guid.NewGuid();
    public string Role => "driver";

    [ReservationTransportFact]
    public async Task PersistedRegistrationAndExactKeyRecoveryWithOfflineOperationsBus()
    {
        using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(105));
        var ct = limit.Token;
        var start = DateTimeOffset.UtcNow.AddHours(1);
        var cmd = new AdmitQuoteReservation(new("51A12345", Guid.NewGuid(), start, start.AddHours(1),
            "PAYOS", Guid.NewGuid()), Guid.NewGuid());
        var fault = new QuoteCommitFault { Enabled = true };
        RegisterReservationDeadline obligation;
        JobKey key;
        await using (var origin = Host(fault))
        {
            // No hosted service or relay has started. Test the same scoped EF bus outbox as HTTP.
            using (var rolledBack = origin.Services.CreateScope())
                await Assert.ThrowsAsync<IOException>(() => rolledBack.ServiceProvider.GetRequiredService<IMediator>().Send(cmd, ct));
            using var scope = origin.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
            Assert.False(await db.ReservationRequests.AnyAsync(r => r.IdempotencyKey == cmd.IdempotencyKey, ct));
            fault.Enabled = false;
            var receipt = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(cmd, ct);
            var d = await db.ReservationRequests.Where(r => r.Id == receipt.RequestId).Select(r => r.OverallDeadline).SingleAsync(ct);
            obligation = new(ReservationDeadlineKind.Admission, receipt.RequestId, d);
            key = new JobKey($"Admission-{receipt.RequestId:N}-{d.UtcTicks}", ReservationDeadlineJob.Group);
            Assert.Single(await db.Set<OutboxMessage>().Where(m => m.CorrelationId == receipt.RequestId &&
                m.MessageType.Contains(nameof(RegisterReservationDeadline))).ToListAsync(ct));
        }

        await using (var online = Host(fault))
        {
            await online.StartAsync(ct);
            var scheduler = await online.Services.GetRequiredService<ISchedulerFactory>().GetScheduler(ct);
            await scheduler.Standby(ct);
            while (await scheduler.GetJobDetail(key, ct) is null) await Task.Delay(100, ct);
            var endpoint = await online.Services.GetRequiredService<IBus>().GetSendEndpoint(new Uri("queue:operations-reservation-deadlines-v1"));
            await endpoint.Send(obligation, c => c.CorrelationId = obligation.SubjectId, ct);
            Assert.Single(await scheduler.GetTriggersOfJob(key, ct));
            await online.StopAsync(ct);
        }

        // Start only the real ADO scheduler. No bus relay runs in this host; the configured broker port is unavailable.
        await using (var offline = Host(fault, offline: true))
        {
            var scheduler = await offline.Services.GetRequiredService<ISchedulerFactory>().GetScheduler(ct);
            fault.Enabled = true;
            await scheduler.Start(ct);
            try
            {
                while ((await scheduler.GetTriggersOfJob(key, ct)).Count != 0) await Task.Delay(200, ct);
                var retained = await scheduler.GetJobDetail(key, ct);
                Assert.NotNull(retained);
                using var scope = offline.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
                var row = db.ReservationRequests.AsNoTracking().Where(r => r.Id == obligation.SubjectId);
                Assert.Equal("CHECKING", await row.Select(r => r.State).SingleAsync(ct));
                fault.Enabled = false;
                await scheduler.TriggerJob(key, cancellationToken: ct);
                while (await scheduler.GetJobDetail(key, ct) is not null) await Task.Delay(100, ct);
                Assert.Equal("ADMISSION_TIMED_OUT", await row.Select(r => r.Code).SingleAsync(ct));
                var wakes = db.Set<OutboxMessage>().Where(m => m.CorrelationId == obligation.SubjectId &&
                    m.MessageType.Contains(nameof(CheckAdmissionDeadline)));
                Assert.Single(await wakes.ToListAsync(ct));
                // Replay the residual job that a crash between business commit and cleanup can leave.
                await scheduler.ScheduleJob(retained!, TriggerBuilder.Create().ForJob(key).StartNow().Build(), cancellationToken: ct);
                while (await scheduler.GetJobDetail(key, ct) is not null) await Task.Delay(100, ct);
                Assert.Single(await wakes.ToListAsync(ct));
            }
            finally { await scheduler.Shutdown(); }
        }
        await using var restored = Host(fault);
        await restored.StartAsync(ct);
        using var verification = restored.Services.CreateScope();
        var store = verification.ServiceProvider.GetRequiredService<ReservationDbContext>();
        while (await store.Set<OutboxMessage>().AnyAsync(m => m.CorrelationId == obligation.SubjectId, ct) ||
            !await store.Set<SmartParking.OperationService.Persistence.Entities.ReservationAdmissionState>()
                .AnyAsync(s => s.CorrelationId == obligation.SubjectId && s.CurrentState == "Final", ct))
            await Task.Delay(100, ct);
        await restored.StopAsync(ct);
    }

    private WebApplication Host(QuoteCommitFault fault, bool offline = false) => OperationHost.Build([], builder =>
    {
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.Configuration["ConnectionStrings:Reservation"] = Environment.GetEnvironmentVariable("SMARTPARK_SAGA_TEST_DB");
        builder.Configuration["SMARTPARK_RABBITMQ_PORT"] = offline ? "1" : Environment.GetEnvironmentVariable("SMARTPARK_SAGA_TEST_BROKER_PORT");
        builder.Configuration["SMARTPARK_RABBITMQ_VHOST"] = Environment.GetEnvironmentVariable("SMARTPARK_SAGA_TEST_VHOST");
        builder.Configuration["ReservationAdmission:BrokerUser"] = Environment.GetEnvironmentVariable("SMARTPARK_SAGA_TEST_USER");
        builder.Configuration["ReservationAdmission:BrokerPassword"] = Environment.GetEnvironmentVariable("SMARTPARK_SAGA_TEST_PASSWORD");
        builder.Configuration["ReservationAdmission:Enabled"] = "true";
        builder.Configuration["ReservationAdmission:SagaConsumersEnabled"] = "true";
        builder.Configuration["Messaging:OutboxDeliveryEnabled"] = "true";
        builder.Configuration["Services:Key"] = "test-only-placeholder-key-for-isolated-host";
        builder.Configuration["JwtValidation:Issuer"] = "urn:test";
        builder.Configuration["JwtValidation:Audience"] = "urn:test";
        builder.Configuration["JwtValidation:JwksUri"] = "https://localhost/unused";
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Host.ConfigureContainer<IServiceCollection>((_, services) =>
        {
            services.AddSingleton<ICurrentActor>(this);
            services.AddScoped<IQuoteAdmissionStarter, AdmissionMessages>();
            services.AddDbContext<ReservationDbContext>(options => options.AddInterceptors(fault));
        });
    });
}
