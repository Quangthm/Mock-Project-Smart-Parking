using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using MassTransit;
using MediatR;
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
using SmartParking.OperationService.API.Middleware;
using SmartParking.OperationService.Application;
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

    [Fact]
    public async Task TypedAdmissionRejectsInvalidAndRetiredInputsBeforeDispatch()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers().AddApplicationPart(typeof(OperationHost).Assembly);
        builder.Services.AddApplicationServices();
        builder.Services.AddExceptionHandler<ReservationExceptionHandler>();
        ApiErrors.Configure(builder.Services);
        builder.Services.AddAuthentication("test")
            .AddScheme<AuthenticationSchemeOptions, AdmissionTestAuthentication>("test", _ => { });
        var dispatch = new AdmissionHttpDispatch();
        builder.Services.AddSingleton<IRequestHandler<AdmitQuoteReservation, ReservationReceipt>>(dispatch);
        builder.Services.AddSingleton<IRequestHandler<PreviewReservationPriceQuery, PricePreview>>(dispatch);
        await using var host = builder.Build();
        ApiErrors.Use(host);
        host.UseAuthentication();
        host.UseAuthorization();
        host.MapControllers();
        await host.StartAsync();
        using var http = new HttpClient { BaseAddress = new Uri(host.Urls.Single()) };
        var intent = new QuoteReservationIntent("51A12345", slot, clock.Now.AddDays(1),
            clock.Now.AddDays(1).AddHours(1), "PAYOS", Guid.NewGuid(), "rq1-test");
        using var unauthenticated = await http.PostAsJsonAsync("/api/reservation-requests", intent);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        http.DefaultRequestHeaders.Add("Test-Account", account.ToString());

        await AssertInvalid(await http.PostAsJsonAsync("/api/reservation-requests", intent));
        http.DefaultRequestHeaders.Add("Idempotency-Key", "not-a-guid");
        await AssertInvalid(await http.PostAsJsonAsync("/api/reservation-requests", intent));
        http.DefaultRequestHeaders.Remove("Idempotency-Key");
        http.DefaultRequestHeaders.Add("Idempotency-Key", key.ToString());
        await AssertInvalid(await http.PostAsJsonAsync("/api/reservation-requests", intent with { QuoteId = Guid.Empty }));
        await AssertInvalid(await http.PostAsJsonAsync("/api/reservation-requests", intent with { QuoteVersion = "" }));
        foreach (var body in new[] { "{", "null", "[]", """{"quoteId":"not-a-guid"}""" })
        {
            using var content = new StringContent(body, System.Text.Encoding.UTF8, "application/json");
            await AssertInvalid(await http.PostAsync("/api/reservation-requests", content));
        }

        var oldIntent = new
        {
            vehicleId = Guid.NewGuid(), siteId = Guid.NewGuid(), requestedSlotId = slot, intent.Start, intent.End,
            intent.PaymentMethod, termsId = Guid.NewGuid(), termsVersion = "old"
        };
        await AssertInvalid(await http.PostAsJsonAsync("/api/reservation-requests", oldIntent));
        using var retired = await http.PostAsJsonAsync("/api/reservation-terms", oldIntent);
        Assert.Equal(HttpStatusCode.NotFound, retired.StatusCode);
        Assert.Equal(0, dispatch.CreationCalls);

        Assert.Equal(dispatch.RequestId, await Submit(http, intent));
        Assert.Equal(1, dispatch.CreationCalls);
        Assert.Equal(intent, dispatch.LastCommand!.Intent);
        Assert.Equal(key, dispatch.LastCommand.IdempotencyKey);
        using var preview = await http.PostAsJsonAsync("/api/reservation-price-previews",
            new ReservationPricePreview(oldIntent.siteId, slot, intent.Start, intent.End, null, null));
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
    }

    [Fact]
    public async Task DisabledAdmissionFailsClosedAndDoesNotRecognizeRetiredTermsRoute()
    {
        await using var host = OperationHost.Build([], builder =>
        {
            builder.Configuration.Sources.Clear();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Reservation"] = "Host=127.0.0.1;Port=1;Database=unused",
                ["Services:Key"] = "test-only-placeholder-key-for-isolated-host"
            });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
        });
        await host.StartAsync();
        using var http = new HttpClient { BaseAddress = new Uri(host.Urls.Single()) };
        using var disabled = await http.PostAsJsonAsync("/api/reservation-requests", new { });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, disabled.StatusCode);
        var failure = await disabled.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("RESERVATION_ADMISSION_DISABLED", failure.GetProperty("code").GetString());
        using var retired = await http.PostAsJsonAsync("/api/reservation-terms", new { });
        Assert.Equal(HttpStatusCode.NotFound, retired.StatusCode);
    }

    private sealed class AdmissionHttpDispatch : IRequestHandler<AdmitQuoteReservation, ReservationReceipt>,
        IRequestHandler<PreviewReservationPriceQuery, PricePreview>
    {
        public Guid RequestId { get; } = Guid.NewGuid();
        public int CreationCalls { get; private set; }
        public AdmitQuoteReservation? LastCommand { get; private set; }

        public Task<ReservationReceipt> Handle(AdmitQuoteReservation command, CancellationToken ct)
        {
            CreationCalls++;
            LastCommand = command;
            return Task.FromResult(new ReservationReceipt(RequestId, "CHECKING", null, null, 3));
        }

        public Task<PricePreview> Handle(PreviewReservationPriceQuery query, CancellationToken ct) =>
            Task.FromResult(new PricePreview(10_000, 10_000, "VND", "test-pricing", false));
    }

    private static async Task AssertInvalid(HttpResponseMessage response)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("INVALID_REQUEST", body.GetProperty("code").GetString());
        }
    }

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
            await AssertInvalid(await http.PostAsJsonAsync("/api/reservation-requests", new
            {
                vehicleId = Guid.NewGuid(), siteId = Guid.NewGuid(), requestedSlotId = slot, intent.Start, intent.End,
                intent.PaymentMethod, termsId = Guid.NewGuid(), termsVersion = "old"
            }));
            using (var rejectedScope = host.Services.CreateScope())
            {
                var rejectedDb = rejectedScope.ServiceProvider.GetRequiredService<ReservationDbContext>();
                Assert.False(await rejectedDb.ReservationRequests.AnyAsync(request => request.AccountId == account));
            }
            var first = await Submit(http, intent);
            using var owningReceipt = await http.GetAsync($"/api/reservation-requests/{first:D}");
            Assert.Equal(HttpStatusCode.OK, owningReceipt.StatusCode);
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
            Assert.Equal(new SlotCheckV1(first, first, account, receipt.Fingerprint, slot, intent.Start, intent.End), facility);
            Assert.Equal(receipt.AttemptId, (await accountCheck.Task).AttemptId);
            var allowed = new AccountAccessDecisionV1(Guid.NewGuid(), first, receipt.AttemptId, 1,
                account, account, "driver", "CREATE_RESERVATION", clock.Now, true, "ALLOWED");
            await Send(bus, "operations-reservation-plate-results-v1",
                new PlateDecisionV1(plate with { AccountId = Guid.NewGuid() }, true, "CAR", 7), first);
            await Send(bus, "operations-reservation-slot-results-v1",
                new SlotDecisionV1(facility, "ELIGIBLE", Guid.NewGuid(), Guid.NewGuid()), first);
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
        var requestId = json.GetProperty("data").GetProperty("requestId").GetGuid();
        Assert.EndsWith($"/api/reservation-requests/{requestId:D}", response.Headers.Location?.OriginalString);
        return requestId;
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
