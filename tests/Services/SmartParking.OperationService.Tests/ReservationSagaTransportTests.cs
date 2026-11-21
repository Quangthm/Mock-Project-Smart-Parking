using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MassTransit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartParking.Contracts.AccountAccess;
using SmartParking.Contracts.ReservationAdmission;
using SmartParking.OperationService.API;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Infrastructure.Reservations;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Persistence.Entities;

namespace SmartParking.OperationService.Tests;

public sealed class ReservationSagaTransportTests
{
    private readonly Guid account = Guid.NewGuid();
    private readonly Guid key = Guid.NewGuid();
    private readonly AdmissionTestClock clock = new(DateTimeOffset.UtcNow);
    private readonly Guid slot = Guid.NewGuid();

    [ReservationTransportFact]
    public async Task DurableCaptureOwnerRepliesTerminalFencingAndRestart()
    {
        var intent = new QuoteReservationIntent("51A-12345", slot, clock.Now.AddDays(1),
            clock.Now.AddDays(1).AddHours(1), "PAYOS", Guid.NewGuid(), "quote-v1");
        Guid pending;
        await using (var host = Build())
        {
            await host.StartAsync();
            using var http = new HttpClient { BaseAddress = new Uri(host.Urls.Single()) };
            http.DefaultRequestHeaders.Add("Test-Account", account.ToString());
            http.DefaultRequestHeaders.Add("Idempotency-Key", key.ToString());
            var bus = host.Services.GetRequiredService<IBus>();
            var accountCheck = new TaskCompletionSource<AccountAccessRequestV1>(TaskCreationOptions.RunContinuationsAsynchronously);
            var plateCheck = new TaskCompletionSource<PlateCheckV1>(TaskCreationOptions.RunContinuationsAsynchronously);
            var slotCheck = new TaskCompletionSource<SlotCheckV1>(TaskCreationOptions.RunContinuationsAsynchronously);
            var ownerAccounts = bus.ConnectReceiveEndpoint(AccountAccessEndpoints.Requests, endpoint =>
                endpoint.Handler<AccountAccessRequestV1>(ctx =>
                {
                    if (ctx.Message.AccountId == account) accountCheck.TrySetResult(ctx.Message);
                    return Task.CompletedTask;
                }));
            var ownerPlates = bus.ConnectReceiveEndpoint("user-reservation-plate-v1", endpoint =>
                endpoint.Handler<PlateCheckV1>(ctx =>
                {
                    if (ctx.Message.AccountId == account) plateCheck.TrySetResult(ctx.Message);
                    return Task.CompletedTask;
                }));
            var ownerSlots = bus.ConnectReceiveEndpoint("facility-reservation-slot-v1", endpoint =>
                endpoint.Handler<SlotCheckV1>(ctx =>
                {
                    if (ctx.Message.AccountId == account) slotCheck.TrySetResult(ctx.Message);
                    return Task.CompletedTask;
                }));
            await Task.WhenAll(ownerAccounts.Ready, ownerPlates.Ready, ownerSlots.Ready);
            var first = await Submit(http, intent);
            Assert.Equal(first, await Submit(http, intent with { LicensePlate = "51A12345" }));
            using var conflict = await http.PostAsJsonAsync("/api/reservation-requests", intent with { QuoteId = Guid.NewGuid() });
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
            await WaitFor(host, first, "Checking");
            using var subscription = new ReservationTestSubscription();
            Assert.True(await subscription.SubscribeAsync(http.BaseAddress!, account, first));
            using var foreign = new ReservationTestSubscription();
            Assert.False(await foreign.SubscribeAsync(http.BaseAddress!, Guid.NewGuid(), first));
            using var scope = host.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
            var receipt = await db.ReservationRequests.AsNoTracking().SingleAsync(x => x.Id == first);
            // No owner replies until all three independent requests have actually been delivered.
            await Task.WhenAll(accountCheck.Task, plateCheck.Task, slotCheck.Task).WaitAsync(TimeSpan.FromSeconds(10));
            var plate = await plateCheck.Task;
            var facility = await slotCheck.Task;
            Assert.Equal(new PlateCheckV1(first, first, account, receipt.Fingerprint, "51A12345"), plate);
            Assert.Equal(new SlotCheckV1(first, first, account, receipt.Fingerprint, slot), facility);
            Assert.Equal(receipt.AttemptId, (await accountCheck.Task).AttemptId);
            var allowed = new AccountAccessDecisionV1(Guid.NewGuid(), first, receipt.AttemptId, 1,
                account, account, "driver", "CREATE_RESERVATION", clock.Now, true, "ALLOWED");
            await Send(bus, "operations-reservation-plate-results-v1",
                new PlateDecisionV1(plate with { AccountId = Guid.NewGuid() }, true, "CAR", 7), first);
            await Send(bus, "operations-reservation-slot-results-v1",
                new SlotDecisionV1(facility, "ELIGIBLE", Guid.NewGuid(), Guid.NewGuid(), "revision"), first);
            await Send(bus, "operations-reservation-plate-results-v1", new PlateDecisionV1(plate, true, "CAR", 7), first);
            await Send(bus, AccountAccessEndpoints.Decisions, allowed, first, receipt.AttemptId);
            await WaitFor(host, first, "Final");
            var outcome = await Snapshot(http, first);
            var delivered = await subscription.UntilTerminalAsync(first);
            Assert.Equal(outcome, delivered);
            Assert.Equal("PAYMENT_QUOTE_PROOF_UNAVAILABLE", outcome.OutcomeCode);
            Assert.Equal(new ReservationChecks("SUCCEEDED", "SUCCEEDED", "SUCCEEDED", "FAILED_DEPENDENCY"), outcome.Checks);
            Assert.Null(outcome.ReservationId);
            await Send(bus, "operations-reservation-plate-results-v1", new PlateDecisionV1(plate, false, null, null), first);
            http.DefaultRequestHeaders.Remove("Test-Account");
            http.DefaultRequestHeaders.Add("Test-Account", Guid.NewGuid().ToString());
            Assert.Equal(HttpStatusCode.NotFound, (await http.GetAsync($"/api/reservation-requests/{first}/progress")).StatusCode);
            http.DefaultRequestHeaders.Remove("Test-Account");
            http.DefaultRequestHeaders.Add("Test-Account", account.ToString());
            http.DefaultRequestHeaders.Remove("Idempotency-Key");
            http.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
            var rejected = await Submit(http, intent);
            await WaitFor(host, rejected, "Checking");
            var refusedPlate = plate with { WorkflowId = rejected, AttemptId = rejected };
            await Send(bus, "operations-reservation-plate-results-v1", new PlateDecisionV1(refusedPlate, false, null, null), rejected);
            await WaitFor(host, rejected, "Final");
            var refused = await Snapshot(http, rejected);
            Assert.Equal("PLATE_INELIGIBLE", refused.OutcomeCode);
            Assert.Null(refused.ReservationId);
            http.DefaultRequestHeaders.Remove("Idempotency-Key");
            http.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
            pending = await Submit(http, intent);
            await WaitFor(host, pending, "Checking");
            Assert.Equal(outcome.Version, (await Snapshot(http, first)).Version);
            foreign.Dispose();
            subscription.Dispose();
            await host.StopAsync();
        }
        clock.Now = clock.Now.AddMinutes(2);
        // A new process has no ambient MassTransit logging scope from the stopped host.
        LogContext.Current = null;
        await using var recovered = Build(enableTestCapture: false);
        await recovered.StartAsync();
        await WaitFor(recovered, pending, "Final");
        using var reader = new HttpClient { BaseAddress = new Uri(recovered.Urls.Single()) };
        reader.DefaultRequestHeaders.Add("Test-Account", account.ToString());
        var expired = await Snapshot(reader, pending);
        Assert.Equal("ADMISSION_TIMED_OUT", expired.OutcomeCode);
        Assert.Null(expired.ReservationId);
        reader.DefaultRequestHeaders.Add("Idempotency-Key", key.ToString());
        var replay = await Submit(reader, intent);
        Assert.NotEqual(pending, replay);
        reader.DefaultRequestHeaders.Remove("Idempotency-Key");
        reader.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        using var gated = await reader.PostAsJsonAsync("/api/reservation-requests", intent);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, gated.StatusCode);
        await recovered.StopAsync();
    }

    private static async Task<Guid> Submit(HttpClient http, QuoteReservationIntent intent)
    {
        using var response = await http.PostAsJsonAsync("/api/reservation-requests", intent);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("data").GetProperty("requestId").GetGuid();
    }

    private static async Task<ReservationProgress> Snapshot(HttpClient http, Guid id)
    {
        var json = await http.GetFromJsonAsync<JsonElement>($"/api/reservation-requests/{id}/progress");
        return json.GetProperty("data").Deserialize<ReservationProgress>(JsonSerializerOptions.Web)!;
    }

    private static async Task Send<T>(IBus bus, string queue, T message, Guid workflow, Guid? envelope = null)
        where T : class
    {
        var endpoint = await bus.GetSendEndpoint(new Uri($"queue:{queue}"));
        await endpoint.Send(message, send => { send.CorrelationId = workflow; send.MessageId = envelope ?? Guid.NewGuid(); });
    }

    private static async Task WaitFor(WebApplication host, Guid id, string state)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        while (!await db.Set<ReservationAdmissionState>().AsNoTracking()
            .AnyAsync(x => x.CorrelationId == id && x.CurrentState == state, timeout.Token))
            await Task.Delay(100, timeout.Token);
    }

    private WebApplication Build(bool enableTestCapture = true) => OperationHost.Build([], builder =>
    {
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.Configuration["ConnectionStrings:Reservation"] = Environment.GetEnvironmentVariable("SMARTPARK_SAGA_TEST_DB");
        builder.Configuration["ReservationAdmission:BrokerUser"] = Environment.GetEnvironmentVariable("SMARTPARK_SAGA_TEST_USER");
        builder.Configuration["ReservationAdmission:BrokerPassword"] = Environment.GetEnvironmentVariable("SMARTPARK_SAGA_TEST_PASSWORD");
        builder.Configuration["SMARTPARK_RABBITMQ_PORT"] = Environment.GetEnvironmentVariable("SMARTPARK_SAGA_TEST_BROKER_PORT");
        builder.Configuration["SMARTPARK_RABBITMQ_VHOST"] = Environment.GetEnvironmentVariable("SMARTPARK_SAGA_TEST_VHOST");
        builder.Configuration["Services:Key"] = "test-only-placeholder-key-for-isolated-host";
        builder.Configuration["ReservationAdmission:Enabled"] = "true";
        builder.Configuration["ReservationAdmission:SagaConsumersEnabled"] = "true";
        builder.Configuration["Messaging:OutboxDeliveryEnabled"] = "true";
        builder.Configuration["JwtValidation:Issuer"] = "urn:test";
        builder.Configuration["JwtValidation:Audience"] = "urn:test";
        builder.Configuration["JwtValidation:JwksUri"] = "https://localhost/unused";
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Host.ConfigureContainer<IServiceCollection>((_, services) =>
        {
            services.AddSingleton<TimeProvider>(clock);
            if (enableTestCapture) services.AddScoped<IQuoteAdmissionStarter, AdmissionMessages>();
            services.AddAuthentication(options => options.DefaultScheme = "test")
                .AddScheme<AuthenticationSchemeOptions, AdmissionTestAuthentication>("test", _ => { });
        });
    });
}
