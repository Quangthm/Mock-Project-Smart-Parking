using System.Text.Json;
using SmartParking.Contracts.ReservationAdmission;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.OperationService.Application;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Infrastructure.Reservations;
using SmartParking.OperationService.Persistence;

namespace SmartParking.OperationService.Tests;

public sealed partial class QuoteBookingDatabaseTests : IReservationPolicyRepository, IReservationQuoteOwner, ICurrentActor,
    IReservationSubscriptionEligibility, IReservationCommercialOwner, IReservationFacilityDependencyOwner
{
    private readonly Guid site = Guid.NewGuid(), tenant = Guid.NewGuid(), slot = Guid.NewGuid();
    private Guid quoteId = Guid.NewGuid();
    private string quoteVersion = "quote-1";
    private readonly Guid platform = Guid.NewGuid(), sitePolicy = Guid.NewGuid(), unit = Guid.NewGuid();
    private readonly Guid order = Guid.NewGuid(), receipt = Guid.NewGuid();
    private readonly string plate = $"51A{Random.Shared.Next(10000, 100000)}";
    private DateTimeOffset start = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds());
    private readonly QuoteCommitFault fault = new();
    private int proofReads;
    private int policyVersion = 1;
    private int holdMinutes = 1;
    private const string SnapshotVersion = "inventory-at-acquire";
    private Func<ReservationQuoteBinding, ReservationQuoteBinding> proofBinding = binding => binding;
    private bool expired, covered;
    private bool wrongReceipt, rejectZero;
    private int reconciledZero;
    private readonly List<Guid> reconciled = [];
    public Guid? AccountId { get; } = Guid.NewGuid();
    public Guid? UserId { get; } = Guid.NewGuid();
    public string Role => "driver";
    private QuoteReservationIntent Intent => new(plate, slot, start, start.AddHours(1), "PAYOS", quoteId, quoteVersion);

    private ServiceProvider Services(ReservationCommercialClient? payment = null, bool listenForPayments = true)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:Reservation"] = Environment.GetEnvironmentVariable("SMARTPARK_QUOTE_TEST_DB")
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices().AddPersistenceServices(config);
        services.AddDbContext<ReservationDbContext>(o => o.AddInterceptors(fault));
        services.AddSingleton<IReservationPolicyRepository>(this);
        services.AddSingleton<IReservationQuoteOwner>(this);
        services.AddSingleton<IReservationCommercialOwner>(this);
        if (payment != null)
        {
            services.AddSingleton<IReservationCommercialOwner>(payment);
            services.AddSingleton<IReservationQuoteOwner>(payment);
        }
        services.AddSingleton<IReservationFacilityDependencyOwner>(this);
        services.AddSingleton<IReservationSubscriptionEligibility>(this);
        services.AddSingleton<ICurrentActor>(this);
        services.AddScoped<IAccountAccessRequester, AccountAccessRequester>();
        services.AddScoped<IAdmissionMessages, AdmissionMessages>();
        services.AddScoped<IQuoteAdmissionStarter, AdmissionMessages>();
        services.AddScoped<IReservationFactsPublisher, ReservationFactsPublisher>();
        services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<ReservationDbContext>(o => {
                o.UsePostgres();
                o.UseBusOutbox(b => b.DisableDeliveryService());
            });
            if (payment == null) x.UsingInMemory((_, _) => { });
            else
            {
                x.AddConsumer<ReservationHoldChangedConsumer>();
                x.AddConsumer<SmartParking.OperationService.Infrastructure.Messaging.ReservationPaymentSucceededConsumer>();
                x.UsingRabbitMq((context, bus) =>
                {
                    bus.Host("127.0.0.1", ushort.Parse(SettlementSetting("PORT")), SettlementSetting("VHOST"), h =>
                    {
                        h.Username(SettlementSetting("OPERATIONS_USER"));
                        h.Password(SettlementSetting("PASSWORD"));
                    });
                    bus.Message<ReservationHoldChangedV1>(m => m.SetEntityName("smartpark.operations.reservation-hold-changed.v1"));
                    bus.ReceiveEndpoint("smartpark-operation-reservation-hold-changed-v1", e =>
                        e.ConfigureConsumer<ReservationHoldChangedConsumer>(context));
                    bus.Message<SmartParking.OperationService.Infrastructure.Messaging.PaymentSucceededV1>(
                        m => m.SetEntityName("smartpark.payment.payment-succeeded.v1"));
                    if (listenForPayments) bus.ReceiveEndpoint("smartpark-operation-reservation-payment-succeeded-v1", e =>
                    {
                        e.UseMessageRetry(r => r.Interval(5, TimeSpan.FromMilliseconds(250)));
                        e.ConfigureConsumer<SmartParking.OperationService.Infrastructure.Messaging.ReservationPaymentSucceededConsumer>(context);
                    });
                });
            }
        });
        return services.BuildServiceProvider();
    }

    public Task<IReadOnlyList<ReservationPolicyRevision>> ReadEffectiveAsync(Guid tenantId, Guid siteId,
        DateTimeOffset at, CancellationToken ct)
    {
        var values = ReservationPolicyResolver.RequiredKeys.ToDictionary(k => k, _ => 1);
        values["MAX_ADVANCE_BOOKING"] = 1440;
        values["RESERVATION_MAX_DURATION"] = 180;
        values["ARRIVAL_LATE_TOLERANCE"] = 15;
        values["PAYMENT_HOLD_DURATION"] = holdMinutes;
        return Task.FromResult<IReadOnlyList<ReservationPolicyRevision>>([
            new(platform, 1, null, null, values, new Dictionary<string, PolicyBound>(), null, "Asia/Ho_Chi_Minh"),
            new(sitePolicy, policyVersion, tenant, site, new Dictionary<string, int>(), new Dictionary<string, PolicyBound>(), false, null)
        ]);
    }

    public PlateCoverage Evaluate(string canonicalPlate, Guid tenantId, Guid siteId, ReservationInterval interval) =>
        new(covered, covered ? "covered-1" : "none-v1");

    public Task<PaymentReservationQuote> ReadProofAsync(Guid account, Guid id, string version, CancellationToken ct)
    {
        Assert.Equal((AccountId!.Value, quoteId, "quote-1"), (account, id, version));
        Interlocked.Increment(ref proofReads);
        var binding = new ReservationQuoteBinding(account, plate, tenant, site, slot, start, Intent.End, "PAYOS",
            Evaluate(plate, tenant, site, new(start, Intent.End)), platform, 1, sitePolicy, 1);
        return Task.FromResult(new PaymentReservationQuote(id, version, "ISSUED", expired ? start.AddDays(-1) : start,
            proofBinding(binding), "tariff-1", 100, covered ? 100 : 0, covered ? 0 : 100, "VND", "FULL", "Full prepayment"));
    }

    public Task<IReadOnlyList<ReservationDepositEvidence>> ReadCollectionsAsync(Guid orderId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ReservationDepositEvidence>>([]);

    public Task<DepositOrderResult> EnsureOrderAsync(DepositOrderRequest request, CancellationToken ct) =>
        Task.FromResult(new DepositOrderResult("CREATED", order, request));
    public Task<ZeroOrderDecision> AcceptZeroAsync(ZeroOrderBinding binding, CancellationToken ct) =>
        Task.FromResult(rejectZero ? new ZeroOrderDecision("REJECTED", binding, null, null, "QUOTE_STALE") :
            new ZeroOrderDecision("ACCEPTED", binding, order, wrongReceipt ? Guid.Empty : receipt));
    public Task ReconcileZeroAsync(ZeroOrderEvidence evidence, string reason, CancellationToken ct)
    {
        Assert.Equal((order, receipt), (evidence.OrderId, evidence.ReceiptId));
        reconciledZero++;
        return Task.CompletedTask;
    }
    public Task ReconcileAsync(Guid reservationId, VerifiedCollection collection, string reason, CancellationToken ct)
    {
        reconciled.Add(collection.CollectionId);
        return Task.CompletedTask;
    }
    public Task<PaymentReservationQuote> IssuePlateQuoteAsync(ReservationQuoteBinding binding, CancellationToken ct) => throw new NotSupportedException();
    public Task<PricePreview> PreviewAsync(Guid account, ReservationPricePreview intent, CancellationToken ct) => throw new NotSupportedException();
    public Task<ZeroPriceAcceptanceResult> AcceptZeroPriceAsync(ZeroPriceAcceptanceRequest request, CancellationToken ct) => throw new NotSupportedException();
    public Task<CollectionLookup> CollectionsAsync(Guid reservationId, Guid orderId, CancellationToken ct) => throw new NotSupportedException();

    private static async Task Send(ServiceProvider services, IRequest command)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command);
    }

    private async Task<Guid> Capture(ServiceProvider services, Guid key)
    {
        using var scope = services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return (await mediator.Send(new AdmitQuoteReservation(Intent, key))).RequestId;
    }

    private async Task<Guid> Prepare(ServiceProvider services, Guid key)
    {
        var id = await Capture(services, key);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var row = await db.ReservationRequests.SingleAsync(r => r.Id == id);
        row.AcceptDecision(row.AttemptId, 1, Guid.NewGuid(), row.AdmittedAt, true, row.AdmittedAt);
        row.RecordPlate(true, plate, 3, row.AdmittedAt);
        row.RecordSlot(true, site, tenant);
        if (!await db.Set<OperationalCapacityConfig>().AnyAsync(q => q.SiteId == site))
            db.Add(new OperationalCapacityConfig { Id = Guid.NewGuid(), SiteId = site, TenantId = tenant,
                VehicleType = "CAR", TotalPhysicalCapacity = 1, MaxReservationQuota = 1, IsActive = true,
                CreatedAt = row.AdmittedAt, UpdatedAt = row.AdmittedAt });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task HoldToken(ServiceProvider services, Guid id, string purpose)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var token = await db.Set<ReservationFacilityDependency>().SingleAsync(t => t.WorkflowId == id && t.Purpose == purpose);
        await Send(services, new AdvanceFacilityDependencyCommand(token.Id));
    }

    public Task<FacilityDependencyResult> AcquireAsync(FacilityDependencyIntent intent, CancellationToken ct)
    {
        Assert.NotEqual("ADMISSION", intent.Purpose);
        Assert.Equal((tenant, site, slot), (intent.TenantId, intent.SiteId, intent.SlotId));
        var snapshot = new FacilityResourceSnapshot(new(tenant, site, slot, SnapshotVersion, true, true),
            [new(slot, unit, "CAR", true, true, false, [unit], ["CAR"])], [], true);
        return Task.FromResult(new FacilityDependencyResult(intent, "HELD", snapshot));
    }

    public Task<FacilityDependencyAcknowledgement> CompleteAsync(FacilityDependencyIntent intent, string outcomeJson, CancellationToken ct)
    {
        Assert.NotEqual("ADMISSION", intent.Purpose);
        return Task.FromResult(new FacilityDependencyAcknowledgement(intent, "COMPLETED", FacilityDependencyProtocol.Fingerprint(outcomeJson)));
    }

    public Task<FacilityDependencyAcknowledgement> AbortAsync(FacilityDependencyIntent intent, CancellationToken ct)
    {
        Assert.NotEqual("ADMISSION", intent.Purpose);
        return Task.FromResult(new FacilityDependencyAcknowledgement(intent, "ABORTED", null));
    }

    [QuoteDatabaseFact]
    public async Task RollbackAndConcurrentQuoteUseCommitOneHoldAndOneOutboxTrigger()
    {
        await using var services = Services();
        var key = Guid.NewGuid();
        var first = await Prepare(services, key);
        var second = await Prepare(services, Guid.NewGuid());
        using var verify = services.CreateScope();
        var db = verify.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var holds = db.Set<OutboxMessage>().Where(m => (m.CorrelationId == first || m.CorrelationId == second) &&
            m.MessageType.Contains("ReservationHoldChangedV1"));
        fault.Enabled = true;
        await Assert.ThrowsAsync<IOException>(() => Send(services, new CommitQuoteHoldCommand(first)));
        fault.Enabled = false;
        var beforeRetry = await db.ReservationRequests.AsNoTracking().SingleAsync(r => r.Id == first);
        Assert.Equal("EVALUATING", beforeRetry.State);
        Assert.Null(beforeRetry.ReservationId);
        Assert.False(await holds.AnyAsync());
        Assert.False(await db.Reservations.AnyAsync(r => r.QuoteId == quoteId));
        Assert.False(await db.Set<ReservationFacilityDependency>().AnyAsync(t => t.WorkflowId == first));
        await Task.WhenAll(Send(services, new CommitQuoteHoldCommand(first)), Send(services, new CommitQuoteHoldCommand(second)));
        var booked = await db.Reservations.AsNoTracking().SingleAsync(r => r.QuoteId == quoteId);
        var winningReceipt = await db.ReservationRequests.AsNoTracking().SingleAsync(r => r.Id == booked.Id);
        Assert.Equal("HOLD_CREATED", winningReceipt.State);
        Assert.Equal(booked.Id, winningReceipt.ReservationId);
        Assert.Equal("PENDING_PAYMENT", booked.Status);
        Assert.Equal(slot, booked.ActiveClaimSlotId);
        Assert.Equal(booked.CreatedAt.AddMinutes(1), booked.HoldExpiresAt);
        Assert.Equal(booked.ExpectedEndTime.AddMinutes(1), booked.ClaimEndTime);
        Assert.Null(booked.TermsId);
        Assert.Null(booked.VehicleId);
        Assert.Null(booked.AcceptedPricingSnapshot);
        Assert.Equal("tariff-1", booked.TariffVersion);
        Assert.Single(await holds.ToListAsync());
        Assert.False(await db.Set<ReservationFacilityDependency>().AnyAsync(t => t.WorkflowId == first || t.WorkflowId == second));
        var loser = first == booked.Id ? second : first;
        Assert.Equal("QUOTE_ALREADY_BOUND", await db.ReservationRequests.Where(r => r.Id == loser).Select(r => r.Code).SingleAsync());
        expired = true;
        var reads = proofReads;
        Assert.Equal(first, await Capture(services, key));
        await Send(services, new CommitQuoteHoldCommand(booked.Id));
        Assert.Equal(reads, proofReads);
        var history = await verify.ServiceProvider.GetRequiredService<IReservationBookingRepository>().FindOwnBookingAsync(AccountId!.Value, booked.Id, default);
        Assert.Equal(plate, history!.Reservation.NormalizedPlate);
    }

    [QuoteDatabaseFact]
    public async Task ExpiredProofOrChangedBindingOrPolicyPreventsHold()
    {
        await using var services = Services();
        foreach (var reason in new[] { "expiry", "site", "tenant", "slot", "start", "end", "account", "plate", "method", "coverage", "policy" })
        {
            proofBinding = binding => binding;
            expired = false;
            policyVersion = 1;
            var id = await Prepare(services, Guid.NewGuid());
            proofBinding = binding => reason switch
            {
                "site" => binding with { SiteId = Guid.NewGuid() },
                "tenant" => binding with { TenantId = Guid.NewGuid() },
                "slot" => binding with { RequestedSlotId = Guid.NewGuid() },
                "start" => binding with { Start = binding.Start.AddMinutes(1) },
                "end" => binding with { End = binding.End.AddMinutes(1) },
                "account" => binding with { AccountId = Guid.NewGuid() },
                "plate" => binding with { CanonicalPlate = "52B12345" },
                "method" => binding with { PaymentMethod = "CASH" },
                "coverage" => binding with { Coverage = binding.Coverage with { Version = "changed" } },
                _ => binding
            };
            expired = reason == "expiry";
            policyVersion = reason == "policy" ? 2 : 1;
            await Send(services, new CommitQuoteHoldCommand(id));
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
            var expected = reason is "expiry" or "policy" or "coverage" ? "QUOTE_STALE" : "OWNER_CONTRACT_INVALID";
            Assert.Equal(expected, await db.ReservationRequests.Where(x => x.Id == id).Select(x => x.Code).SingleAsync());
            Assert.False(await db.Set<ReservationFacilityDependency>().AnyAsync(x => x.WorkflowId == id));
            Assert.False(await db.Reservations.AnyAsync(x => x.Id == id));
        }
    }
    [QuoteDatabaseFact]
    public async Task OnlyTheRequestedIntervalCanResolveThePendingSlotBranch()
    {
        await using var services = Services();
        var id = await Capture(services, Guid.NewGuid());
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var row = await db.ReservationRequests.AsNoTracking().SingleAsync(r => r.Id == id);
        var expected = new SlotCheckV1(id, id, row.AccountId, row.Fingerprint, slot, start, Intent.End);
        await mediator.Send(new ProcessAdmissionSignal(id, new StartQuoteAdmission(id)));
        var dispatched = await db.Set<OutboxMessage>().SingleAsync(m => m.CorrelationId == id && m.MessageType.Contains("SlotCheckV1"));
        using var envelope = JsonDocument.Parse(dispatched.Body);
        Assert.Equal(expected, envelope.RootElement.GetProperty("message").Deserialize<SlotCheckV1>(JsonSerializerOptions.Web));

        var oldRequest = JsonSerializer.SerializeToNode(expected)!.AsObject();
        oldRequest.Remove("Start");
        oldRequest.Remove("End");
        var oldEcho = oldRequest.Deserialize<SlotCheckV1>()!;
        foreach (var echo in new[] { oldEcho, expected with { Start = default }, expected with { End = default },
            expected with { Start = expected.End }, expected with { End = expected.End.AddMinutes(1) } })
            await mediator.Send(new ProcessAdmissionSignal(id, new SlotDecisionV1(echo, "ELIGIBLE", site, tenant)));
        Assert.Equal("PENDING", await db.ReservationRequests.Where(r => r.Id == id).Select(r => r.SlotCheck).SingleAsync());

        await mediator.Send(new ProcessAdmissionSignal(id, new SlotDecisionV1(expected, "INELIGIBLE", null, null)));
        await mediator.Send(new ProcessAdmissionSignal(id, new SlotDecisionV1(expected, "ELIGIBLE", site, tenant)));
        await Send(services, new CommitQuoteHoldCommand(id));
        Assert.Equal("SLOT_INELIGIBLE", await db.ReservationRequests.Where(r => r.Id == id).Select(r => r.Code).SingleAsync());
        Assert.False(await db.Reservations.AnyAsync(r => r.Id == id));

        var next = await Capture(services, Guid.NewGuid());
        var pending = await db.ReservationRequests.AsNoTracking().SingleAsync(r => r.Id == next);
        var decision = new SlotDecisionV1(expected with { WorkflowId = next, AttemptId = next, Fingerprint = pending.Fingerprint },
            "ELIGIBLE", site, tenant);
        Assert.False(JsonSerializer.SerializeToElement(decision, JsonSerializerOptions.Web).TryGetProperty("version", out _));
        await mediator.Send(new ProcessAdmissionSignal(next, decision));
        await mediator.Send(new ProcessAdmissionSignal(next, decision with { SiteId = Guid.NewGuid() }));
        var accepted = await db.ReservationRequests.AsNoTracking().SingleAsync(r => r.Id == next);
        Assert.Equal("SUCCEEDED", accepted.SlotCheck);
        Assert.Equal(site, accepted.ResolvedSiteId);
        Assert.Equal(tenant, accepted.ResolvedTenantId);
    }

    [QuoteDatabaseFact]
    public async Task AcceptedSlotHoldDoesNotReadFacilityInventory()
    {
        await using var services = Services();
        var original = await ReadProofAsync(AccountId!.Value, quoteId, quoteVersion, default);
        var id = await Prepare(services, Guid.NewGuid());
        await Send(services, new CommitQuoteHoldCommand(id));
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var held = await db.Reservations.SingleAsync(r => r.Id == id);
        Assert.Equal(original.Binding.RequestedSlotId, held.ActiveClaimSlotId);
        Assert.Equal(original.QuoteId, held.QuoteId);
        Assert.Equal(original.DepositAmount, held.DepositAmount);
        Assert.False(await db.Set<ReservationFacilityDependency>().AnyAsync(x => x.WorkflowId == id));
    }

    [QuoteDatabaseFact]
    public async Task QuotedHoldDeadlineIsCappedAtReservationStart()
    {
        start = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddMinutes(3).ToUnixTimeSeconds());
        holdMinutes = 5;
        await using var services = Services();
        var id = await Prepare(services, Guid.NewGuid());
        await Send(services, new CommitQuoteHoldCommand(id));
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var held = await db.Reservations.SingleAsync(reservation => reservation.Id == id);
        Assert.Equal(start, held.HoldExpiresAt);
    }

    [QuoteDatabaseFact]
    public async Task LocalStructureEditDefersHoldUntilReleased()
    {
        await using var services = Services();
        var id = await Prepare(services, Guid.NewGuid());
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var hold = new StructureEditHoldRow { SiteId = site, TenantId = tenant, Token = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
        var state = new StructureSiteStateRow { SiteId = site, TenantId = tenant, Active = false };
        db.AddRange(hold, state);
        await db.SaveChangesAsync();
        await Send(services, new CommitQuoteHoldCommand(id));
        var deferred = await db.ReservationRequests.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(("EVALUATING", "STRUCTURE_BUSY"), (deferred.State, deferred.Code));
        Assert.False(await db.Reservations.AnyAsync(r => r.QuoteId == quoteId));
        db.Remove(hold);
        state.Active = true;
        await db.SaveChangesAsync();
        await Send(services, new CommitQuoteHoldCommand(id));
        Assert.Equal("HOLD_CREATED", await db.ReservationRequests.Where(r => r.Id == id).Select(r => r.State).SingleAsync());
        Assert.False(await db.Set<ReservationFacilityDependency>().AnyAsync(x => x.WorkflowId == id));
    }

    private async Task<Guid> SeedTimedHold(ServiceProvider services, bool late, bool zero)
    {
        covered = zero;
        var key = Guid.NewGuid();
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var created = now.AddMinutes(-5);
        var h = late ? now.AddSeconds(-1) : now.AddMinutes(2);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var request = ReservationRequest.AdmitQuote(UserId!.Value, AccountId!.Value, key, JsonSerializer.Serialize(Intent),
            "seeded-timed-hold", created);
        request.AcceptDecision(request.AttemptId, 1, Guid.NewGuid(), created, true, created);
        request.RecordPlate(true, plate, 1, created);
        request.RecordSlot(true, site, tenant);
        var policy = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new ResolveReservationPoliciesQuery(tenant, site, created));
        var booking = ReservationRow.PrepareQuote(request.Id, request.AccountId, plate, tenant, site, slot, "PAYOS",
            start, Intent.End, Intent.End.AddMinutes(1), created, h, 100, zero ? 100 : 0, zero ? 0 : 100, "VND",
            JsonSerializer.Serialize(policy), quoteId, "quote-1", start, "tariff-1", zero ? "covered-1" : "none-v1", zero);
        request.AttachQuoteHold(booking.Id);
        if (!zero)
        {
            booking.BeginOrderProvisioning(created, TimeSpan.FromSeconds(1));
            booking.RecordOrderCreated(order);
        }
        else if (late)
        {
            // Simulate the original owner committing before H and losing its acknowledgement.
            booking.BeginZeroPriceAcceptance(created, TimeSpan.FromSeconds(1));
            booking.DeferZeroPriceAcceptance(created.AddSeconds(2), "UNKNOWN", "RESPONSE_LOST");
        }
        db.Add(request);
        db.Add(booking);
        db.Add(new OperationalCapacityConfig { Id = Guid.NewGuid(), SiteId = site, TenantId = tenant, VehicleType = "CAR",
            TotalPhysicalCapacity = 1, MaxReservationQuota = 1, IsActive = true, CreatedAt = created, UpdatedAt = created });
        await db.SaveChangesAsync();
        return booking.Id;
    }

    private ReservationDepositEvidence Evidence(Guid id) => new(Guid.NewGuid(), Guid.NewGuid(), order, Guid.NewGuid(),
        id, id, AccountId!.Value, tenant, site, quoteId, "quote-1", null, null, 100, "VND", "PAYOS", "verified-reference",
        DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "PAYOS");

    [QuoteDatabaseFact]
    public async Task VerifiedCollectionConfirmsOnceAndAdditionalMoneyIsReconciled()
    {
        await using var services = Services();
        var id = await Prepare(services, Guid.NewGuid());
        await Send(services, new CommitQuoteHoldCommand(id));
        await Send(services, new ProvisionReservationOrderCommand(id));
        var evidence = Evidence(id);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Send(services, new RecordReservationPaymentCommand(new(Guid.NewGuid(), order, Guid.NewGuid(), id,
            AccountId!.Value, tenant, site, 100, "VND", "PAYOS", "generic", start))));
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        Task<ReservationRow> Read() => db.Reservations.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Null((await Read()).CollectionId);
        await Send(services, new RecordReservationDepositCommand(evidence with { PaymentMethod = "CASH" }));
        Assert.Null((await Read()).CollectionId);
        Assert.Single(reconciled);
        await Send(services, new RecordReservationDepositCommand(evidence));
        await HoldToken(services, id, "CONFIRMATION");
        await Send(services, new ConfirmPaidReservationCommand(id, evidence.CollectionId));
        var confirmed = await Read();
        Assert.Equal("CONFIRMED", confirmed.Status);
        Assert.True(confirmed.ConfirmationEvaluatedAt < confirmed.HoldExpiresAt);
        await Send(services, new RecordReservationDepositCommand(evidence with { EventId = Guid.NewGuid() }));
        await Send(services, new ConfirmPaidReservationCommand(id, evidence.CollectionId));
        Assert.Single(reconciled);
        var other = Evidence(id);
        await Send(services, new RecordReservationDepositCommand(other));
        Assert.Equal(other.CollectionId, reconciled.Last());
        Assert.Equal(confirmed.ConfirmationEffectId, (await Read()).ConfirmationEffectId);
    }

    [QuoteDatabaseFact]
    public async Task PaidAfterExpiryReacquiresAnAvailableClaim()
    {
        await using var services = Services();
        var id = await SeedTimedHold(services, true, false);
        var payment = Evidence(id);
        policyVersion++; // A compatible revision still permits the original interval and buffer.
        var expiry = Send(services, new ExpireReservationHoldCommand(id));
        try { await Send(services, new RecordReservationDepositCommand(payment)); }
        catch (InvalidOperationException error) when (error.Message.Contains("busy booking"))
        {
            await expiry;
            await Send(services, new RecordReservationDepositCommand(payment));
        }
        await expiry;
        await Send(services, new ConfirmPaidReservationCommand(id, payment.CollectionId));
        await HoldToken(services, id, "CONFIRMATION");
        await Send(services, new ConfirmPaidReservationCommand(id, payment.CollectionId));
        using var scope = services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<ReservationDbContext>().Reservations.SingleAsync(r => r.Id == id);
        Assert.Equal("CONFIRMED", row.Status);
        Assert.True(row.ConfirmationEvaluatedAt >= row.HoldExpiresAt && row.ConfirmationEvaluatedAt < row.ExpectedStartTime);
    }

    [QuoteDatabaseFact]
    public async Task CompetingSameSiteReservationAfterExpirySendsPaidMoneyToReconciliation()
    {
        await using var services = Services();
        var id = await SeedTimedHold(services, true, false);
        var payment = Evidence(id);
        await Send(services, new ExpireReservationHoldCommand(id));
        await Send(services, new RecordReservationDepositCommand(payment));
        await HoldToken(services, id, "CONFIRMATION");
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO reservations (tenant_id,site_id,account_id,normalized_plate,status,target_slot_id,expected_start_time,expected_end_time) VALUES ({tenant},{site},{Guid.NewGuid()},{plate},'CONFIRMED',{Guid.NewGuid()},{start},{start.AddHours(1)})");
        await Send(services, new ConfirmPaidReservationCommand(id, payment.CollectionId));
        var row = await db.Reservations.AsNoTracking().SingleAsync(r => r.Id == id);
        Assert.Equal("EXPIRED", row.Status);
        Assert.Equal("RECONCILIATION_REQUIRED", row.ConfirmationState);
        Assert.Null(row.ActiveClaimSlotId);
        await Send(services, new ConfirmPaidReservationCommand(id, payment.CollectionId));
        Assert.Equal([payment.CollectionId], reconciled);
    }

    [QuoteDatabaseFact]
    public async Task CoveredAcceptanceRequiresMatchingOrderAndDurableReceiptBeforeH()
    {
        await using var services = Services();
        covered = true;
        var id = await Prepare(services, Guid.NewGuid());
        await Send(services, new CommitQuoteHoldCommand(id));
        await Send(services, new AcceptZeroPriceTermsCommand(id));
        await Send(services, new ConfirmZeroPriceReservationCommand(id));
        await HoldToken(services, id, "CONFIRMATION");
        await Send(services, new ConfirmZeroPriceReservationCommand(id));
        await Send(services, new AcceptZeroPriceTermsCommand(id));
        using var scope = services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<ReservationDbContext>().Reservations.SingleAsync(r => r.Id == id);
        Assert.Equal("CONFIRMED", row.Status);
        Assert.Equal(order, row.PaymentOrderId);
        Assert.Equal(receipt, row.ZeroReceiptId);
        Assert.Null(row.CollectionId);
        Assert.True(row.ConfirmationEvaluatedAt < row.HoldExpiresAt);
    }

    [QuoteDatabaseFact]
    public async Task LateCoveredReplayReconcilesItsOriginalOrderWithoutRestoringCapacity()
    {
        await using var services = Services();
        var id = await SeedTimedHold(services, true, true);
        await Send(services, new ExpireReservationHoldCommand(id));
        await Send(services, new AcceptZeroPriceTermsCommand(id));
        await Send(services, new ConfirmZeroPriceReservationCommand(id));
        await Send(services, new ConfirmZeroPriceReservationCommand(id));
        using var scope = services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<ReservationDbContext>().Reservations.SingleAsync(r => r.Id == id);
        Assert.Equal(("EXPIRED", "NO_ENTITLEMENT"), (row.Status, row.ConfirmationState));
        Assert.Null(row.ConfirmationEffectId);
        Assert.Null(row.ActiveClaimSlotId);
        Assert.Equal(1, reconciledZero);
    }
    [QuoteDatabaseFact]
    public async Task HistoricalVehicleTermsHoldProvisionsAndConfirmsFromTheLegacyPaymentWire()
    {
        await using var services = Services();
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var now = DateTimeOffset.UtcNow;
        var requestId = Guid.NewGuid();
        var termsId = Guid.NewGuid();
        var vehicle = Guid.NewGuid();
        var accountId = AccountId!.Value;
        const string termsVersion = "terms-1";
        var selection = new BookingSelection(vehicle, site, slot, start, Intent.End, "PAYOS");
        var commercial = new CommercialTerms(quoteId, "legacy-payment-v1", start, selection, accountId, 100, 100,
            "VND", "FULL", "standard-v1", "Full prepayment");
        var policy = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new ResolveReservationPoliciesQuery(tenant, site, now));
        var policyJson = JsonSerializer.Serialize(policy);

        // Model a booking committed by a previous deployment without calling a retired writer.
        await db.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO reservation_requests
                (id, user_id, account_id, idempotency_key, operation, intent_json, fingerprint, state, admitted_at,
                 overall_deadline, attempt_id, attempt_number, attempt_started_at, attempt_deadline, next_action_at,
                 decision_id, decided_at, awaiting_reply, reservation_id, vehicle_binding_state, vehicle_plate)
            VALUES
                ({{requestId}}, {{UserId!.Value}}, {{accountId}}, {{Guid.NewGuid()}}, 'CREATE_RESERVATION', '{}', 'legacy-intent',
                 'HOLD_CREATED', {{now}}, {{now.AddSeconds(90)}}, {{Guid.NewGuid()}}, 1, {{now}}, {{now.AddSeconds(5)}},
                 {{now.AddSeconds(5)}}, {{Guid.NewGuid()}}, {{now}}, false, {{requestId}}, 'ACTIVE', {{plate}});

            INSERT INTO reservation_terms
                (id, account_id, intent_fingerprint, version, valid_until, snapshot_json, accepted_request_id)
            VALUES ({{termsId}}, {{accountId}}, 'legacy-selection', {{termsVersion}}, {{start}}, '{}', {{requestId}});

            INSERT INTO reservations
                (id, request_id, account_id, tenant_id, site_id, vehicle_id, target_slot_id, active_claim_slot_id,
                 terms_id, terms_version, selected_payment_method, expected_start_time, expected_end_time, claim_end_time,
                 created_at, hold_expires_at, original_plate, normalized_plate, booked_amount, deposit_amount, currency,
                 accepted_operational_policy_snapshot, accepted_pricing_snapshot, order_provisioning_state,
                 order_provisioning_next_action_at)
            VALUES
                ({{requestId}}, {{requestId}}, {{accountId}}, {{tenant}}, {{site}}, {{vehicle}}, {{slot}}, {{slot}}, {{termsId}},
                 {{termsVersion}}, 'PAYOS', {{start}}, {{Intent.End}}, {{Intent.End.AddMinutes(1)}}, {{now}}, {{now.AddMinutes(5)}},
                 {{plate}}, {{plate}}, 100, 100, 'VND', CAST({{policyJson}} AS jsonb),
                 CAST({{JsonSerializer.Serialize(commercial)}} AS jsonb), 'PENDING', {{now}});
            """);
        db.Add(new OperationalCapacityConfig { Id = Guid.NewGuid(), TenantId = tenant, SiteId = site, VehicleType = "CAR",
            TotalPhysicalCapacity = 1, MaxReservationQuota = 1, IsActive = true, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();
        await Send(services, new ProvisionReservationOrderCommand(requestId));
        var transaction = Guid.NewGuid();
        var signal = new RecordReservationPaymentCommand(new(Guid.NewGuid(), order, transaction, requestId,
            accountId, tenant, site, 100, "VND", "PAYOS", "legacy-ref", now));
        await Send(services, signal);
        await HoldToken(services, requestId, "CONFIRMATION");
        await Send(services, new ConfirmPaidReservationCommand(requestId, transaction));
        await Send(services, signal);
        var persisted = await db.Reservations.AsNoTracking().SingleAsync(b => b.Id == requestId);
        Assert.Equal("CONFIRMED", persisted.Status);
        Assert.Equal(termsId, persisted.TermsId);
        Assert.Equal(vehicle, persisted.VehicleId);
        Assert.Null(persisted.QuoteId);
        Assert.Equal(0, proofReads);
    }
    [QuoteDatabaseFact]
    public async Task CoveredResponseWithoutReceiptRemainsUnknownAndCannotConfirm()
    {
        await using var services = Services();
        var id = await SeedTimedHold(services, false, true);
        wrongReceipt = true;
        await Send(services, new AcceptZeroPriceTermsCommand(id));
        await Send(services, new ConfirmZeroPriceReservationCommand(id));
        using var scope = services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<ReservationDbContext>().Reservations.SingleAsync(r => r.Id == id);
        Assert.Equal("UNKNOWN", row.ZeroPriceAcceptanceState);
        Assert.Equal("PENDING_PAYMENT", row.Status);
        Assert.Null(row.ZeroReceiptId);
        Assert.Null(row.PaymentOrderId);
        Assert.Null(row.ConfirmationEffectId);
    }
    [QuoteDatabaseFact]
    public async Task DefinitiveZeroOrderRejectionReleasesTheClaim()
    {
        await using var services = Services();
        var id = await SeedTimedHold(services, false, true);
        rejectZero = true;
        await Send(services, new AcceptZeroPriceTermsCommand(id));
        using var scope = services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<ReservationDbContext>().Reservations.SingleAsync(r => r.Id == id);
        Assert.Equal(("EXPIRED", "REJECTED", "QUOTE_STALE"), (row.Status, row.ZeroPriceAcceptanceState, row.ConfirmationCode));
        Assert.Null(row.ActiveClaimSlotId);
        Assert.Null(row.PaymentOrderId);
    }
}
