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
    IReservationSubscriptionEligibility, IReservationCommercialOwner
{
    private readonly Guid site = Guid.NewGuid(), tenant = Guid.NewGuid(), slot = Guid.NewGuid();
    private Guid quoteId = Guid.NewGuid();
    private readonly Guid platform = Guid.NewGuid(), sitePolicy = Guid.NewGuid(), unit = Guid.NewGuid();
    private readonly Guid order = Guid.NewGuid(), receipt = Guid.NewGuid();
    private readonly string plate = $"51A{Random.Shared.Next(10000, 100000)}";
    private DateTimeOffset start = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds());
    private readonly QuoteCommitFault fault = new();
    private int proofReads;
    private int policyVersion = 1;
    private int holdMinutes = 1;
    private Func<ReservationQuoteBinding, ReservationQuoteBinding> proofBinding = binding => binding;
    private bool expired, covered;
    private readonly List<Guid> reconciled = [];
    public Guid? AccountId { get; } = Guid.NewGuid();
    public Guid? UserId { get; } = Guid.NewGuid();
    public string Role => "driver";
    private QuoteReservationIntent Intent => new(plate, slot, start, start.AddHours(1), "PAYOS", quoteId);

    private ServiceProvider Services(ReservationCommercialClient? payment = null, bool listenForPayments = true)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ConnectionStrings:Reservation"] = payment == null ? Environment.GetEnvironmentVariable("SMARTPARK_QUOTE_TEST_DB") : SettlementSetting("OPERATIONS_DB")
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
                o.QueryDelay = TimeSpan.FromMilliseconds(100);
                o.UseBusOutbox(b => { if (payment == null) b.DisableDeliveryService(); });
            });
            if (payment == null) x.UsingInMemory((_, _) => { });
            else
            {
                x.AddConsumer<SmartParking.OperationService.Infrastructure.Messaging.ReservationZeroReceiptIssuedConsumer>();
                x.AddConsumer<SmartParking.OperationService.Infrastructure.Messaging.ReservationPaymentSucceededConsumer>();
                x.UsingRabbitMq((context, bus) =>
                {
                    bus.Host("127.0.0.1", ushort.Parse(SettlementSetting("PORT")), SettlementSetting("VHOST"), h =>
                    {
                        h.Username(SettlementSetting("OPERATIONS_USER"));
                        h.Password(SettlementSetting("PASSWORD"));
                    });
                    bus.ReceiveEndpoint("operations-reservation-zero-receipt-v1", e =>
                    {
                        e.ConfigureConsumeTopology = false;
                        e.UseEntityFrameworkOutbox<ReservationDbContext>(context);
                        e.ConfigureConsumer<SmartParking.OperationService.Infrastructure.Messaging.ReservationZeroReceiptIssuedConsumer>(context);
                    });
                    bus.Message<SmartParking.OperationService.Infrastructure.Messaging.PaymentSucceededV1>(
                        m => m.SetEntityName("smartpark.payment.payment-succeeded.v1"));
                    if (listenForPayments) bus.ReceiveEndpoint("smartpark-operation-reservation-payment-succeeded-v1", e =>
                    {
                        e.UseMessageRetry(r => r.Interval(5, TimeSpan.FromMilliseconds(250)));
                        e.UseEntityFrameworkOutbox<ReservationDbContext>(context);
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

    public Task<PaymentReservationQuote> ReadProofAsync(Guid account, Guid id, CancellationToken ct)
    {
        Assert.Equal((AccountId!.Value, quoteId), (account, id));
        Interlocked.Increment(ref proofReads);
        var binding = new ReservationQuoteBinding(account, plate, tenant, site, slot, start, Intent.End, "PAYOS",
            Evaluate(plate, tenant, site, new(start, Intent.End)), platform, 1, sitePolicy, 1);
        return Task.FromResult(new PaymentReservationQuote(id, "ISSUED", expired ? start.AddDays(-1) : start,
            proofBinding(binding), "tariff-1", 100, covered ? 100 : 0, covered ? 0 : 100, "VND", "FULL", "Full prepayment"));
    }

    public Task ReconcileAsync(Guid reservationId, VerifiedCollection collection, string reason, CancellationToken ct)
    {
        reconciled.Add(collection.CollectionId);
        return Task.CompletedTask;
    }
    public Task<PaymentReservationQuote> IssuePlateQuoteAsync(ReservationQuoteBinding binding, CancellationToken ct) => throw new NotSupportedException();
    public Task<PricePreview> PreviewAsync(Guid account, ReservationPricePreview intent, CancellationToken ct) => throw new NotSupportedException();
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
        var original = await ReadProofAsync(AccountId!.Value, quoteId, default);
        var id = await Prepare(services, Guid.NewGuid());
        await Send(services, new CommitQuoteHoldCommand(id));
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var held = await db.Reservations.SingleAsync(r => r.Id == id);
        Assert.Equal(original.Binding.RequestedSlotId, held.ActiveClaimSlotId);
        Assert.Equal(original.QuoteId, held.QuoteId);
        Assert.Equal(original.DepositAmount, held.DepositAmount);
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
    }

    private async Task<Guid> SeedTimedHold(ServiceProvider services, bool late, bool zero)
    {
        covered = zero;
        var key = Guid.NewGuid();
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var created = now.AddMinutes(-5);
        var h = late ? now.AddMinutes(-2) : now.AddMinutes(2);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var request = ReservationRequest.AdmitQuote(UserId!.Value, AccountId!.Value, key, JsonSerializer.Serialize(Intent),
            "seeded-timed-hold", created);
        request.AcceptDecision(request.AttemptId, 1, Guid.NewGuid(), created, true, created);
        request.RecordPlate(true, plate, 1, created);
        request.RecordSlot(true, site, tenant);
        var policy = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new ResolveReservationPoliciesQuery(tenant, site, created));
        var booking = ReservationRow.PrepareQuote(request.Id, request.AccountId, plate, tenant, site, slot, "PAYOS",
            start, Intent.End, Intent.End.AddMinutes(1), created, zero ? null : h, 100, zero ? 100 : 0, zero ? 0 : 100, "VND",
            JsonSerializer.Serialize(policy), quoteId, start, "tariff-1", zero ? "covered-1" : "none-v1", zero);
        if (zero) booking.ConfirmZeroAdmission(created);
        request.CompleteAdmission(booking.Id, zero);
        db.Add(request);
        db.Add(booking);
        db.Add(new OperationalCapacityConfig { Id = Guid.NewGuid(), SiteId = site, TenantId = tenant, VehicleType = "CAR",
            TotalPhysicalCapacity = 1, MaxReservationQuota = 1, IsActive = true, CreatedAt = created, UpdatedAt = created });
        await db.SaveChangesAsync();
        return booking.Id;
    }

    private ReservationPaymentEvidence Evidence(Guid id) => new(Guid.NewGuid(), order, Guid.NewGuid(), id,
        AccountId!.Value, tenant, site, 100, "VND", "PAYOS", plate, DateTimeOffset.UtcNow);

    [QuoteDatabaseFact]
    public async Task SuccessfulSettlementBindsDirectlyAndSurvivesReplay()
    {
        await using var services = Services();
        var id = await Prepare(services, Guid.NewGuid());
        await Send(services, new CommitQuoteHoldCommand(id));
        var evidence = Evidence(id);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        Task<ReservationRow> Read() => db.Reservations.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Null((await Read()).PaymentOrderId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Send(services, new RecordReservationPaymentCommand(evidence with { PaymentMethod = "CASH" })));
        await Send(services, new RecordReservationPaymentCommand(evidence));
        var confirmed = await Read();
        Assert.Equal(("CONFIRMED", order, evidence.PaymentTransactionId), (confirmed.Status, confirmed.PaymentOrderId, confirmed.CollectionId));
        await Send(services, new RecordReservationPaymentCommand(evidence with {EventId = Guid.NewGuid()}));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Send(services, new RecordReservationPaymentCommand(Evidence(id))));
        Assert.Equal(confirmed.ConfirmationEffectId, (await Read()).ConfirmationEffectId);
    }

    [QuoteDatabaseFact]
    public async Task LatePaidClaimReacquiresOriginalSlot()
    {
        await using var services = Services();
        var id = await SeedTimedHold(services, true, false);
        await Send(services, new ExpireReservationHoldCommand(id));
        await Send(services, new RecordReservationPaymentCommand(Evidence(id)));
        using var scope = services.CreateScope();
        var row = await scope.ServiceProvider.GetRequiredService<ReservationDbContext>().Reservations.SingleAsync(r => r.Id == id);
        Assert.Equal(("CONFIRMED", slot), (row.Status, row.ActiveClaimSlotId));
        Assert.True(row.ConfirmationEvaluatedAt >= row.HoldExpiresAt);
    }

    [QuoteDatabaseFact]
    public async Task FirstCollectionAfterStartBindsWithoutEntitlementAndStagesOneDisposition()
    {
        start = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddSeconds(-10).ToUnixTimeSeconds());
        await using var services = Services();
        var id = await SeedTimedHold(services, true, false);
        var evidence = Evidence(id);
        await Send(services, new RecordReservationPaymentCommand(evidence));
        await Send(services, new RecordReservationPaymentCommand(evidence));
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var row = await db.Reservations.SingleAsync(r => r.Id == id);
        Assert.Equal(("EXPIRED", "RESERVATION_START_PASSED", evidence.PaymentOrderId),
            (row.Status, row.ConfirmationCode, row.PaymentOrderId));
        Assert.Null(row.ConfirmationEffectId);
        Assert.Null(row.ActiveClaimSlotId);
        Assert.Single(await db.Set<OutboxMessage>().Where(m => m.MessageType.Contains("RequestReservationDispositionV1")
            && m.Body.Contains(id.ToString())).ToListAsync());
    }

    [QuoteDatabaseFact]
    public async Task LatePaidConflictCommitsOneDisposition()
    {
        await using var services = Services();
        var id = await SeedTimedHold(services, true, false);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO reservations (tenant_id,site_id,account_id,normalized_plate,status,target_slot_id,expected_start_time,expected_end_time) VALUES ({tenant},{site},{Guid.NewGuid()},{plate},'CONFIRMED',{Guid.NewGuid()},{start},{start.AddHours(1)})");
        var evidence = Evidence(id);
        await Send(services, new RecordReservationPaymentCommand(evidence));
        await Send(services, new RecordReservationPaymentCommand(evidence));
        var row = await db.Reservations.SingleAsync(r => r.Id == id);
        Assert.Equal(("INVALIDATED", "NO_ENTITLEMENT", evidence.PaymentTransactionId), (row.Status, row.ConfirmationState, row.CollectionId));
        Assert.Null(row.ActiveClaimSlotId);
        Assert.Single(await db.Set<OutboxMessage>().Where(m => m.MessageType.Contains("RequestReservationDispositionV1") && m.Body.Contains(id.ToString())).ToListAsync());
    }

    [QuoteDatabaseFact]
    public async Task ZeroAdmissionAndFinancialLinkHaveIndependentAtomicEffects()
    {
        covered = true;
        await using var services = Services();
        var id = await Prepare(services, Guid.NewGuid());
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        var effects = db.Set<OutboxMessage>().Where(m => m.Body.Contains(id.ToString()));
        fault.Enabled = true;
        await Assert.ThrowsAsync<IOException>(() => Send(services, new CommitQuoteHoldCommand(id)));
        fault.Enabled = false;
        Assert.False(await db.Reservations.AnyAsync(r => r.Id == id));
        Assert.False(await effects.AnyAsync(m => m.MessageType.Contains("EnsureReservationOrderV1") || m.MessageType.Contains("ReservationConfirmedV1")));
        await Send(services, new CommitQuoteHoldCommand(id));
        Task<ReservationRow> Read() => db.Reservations.AsNoTracking().SingleAsync(r => r.Id == id);
        var admitted = await Read();
        Assert.Equal(("CONFIRMED", slot), (admitted.Status, admitted.ActiveClaimSlotId));
        Assert.Equal("CONFIRMED", await db.ReservationRequests.Where(r => r.Id == id).Select(r => r.State).SingleAsync());
        Assert.Null(admitted.HoldExpiresAt);
        Assert.Null(admitted.PaymentOrderId);
        Assert.Null(admitted.ZeroReceiptId);
        Assert.Null(admitted.CollectionId);
        Assert.Single(await effects.Where(m => m.MessageType.Contains("EnsureReservationOrderV1")).ToListAsync());
        db.Add(new StructureSiteStateRow { SiteId = site, TenantId = tenant, Active = false });
        await db.SaveChangesAsync();
        var link = new LinkReservationZeroReceipt(new(id, AccountId!.Value, tenant, site, quoteId, order, receipt, 0, "VND"));
        await Send(services, link);
        await Send(services, link);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Send(services, link with { Evidence = link.Evidence with { ReceiptId = Guid.NewGuid() } }));
        var linked = await Read();
        Assert.Equal((order, receipt, admitted.ConfirmedAt, admitted.ConfirmationEffectId, slot),
            (linked.PaymentOrderId, linked.ZeroReceiptId, linked.ConfirmedAt, linked.ConfirmationEffectId, linked.ActiveClaimSlotId));
        Assert.Single(await effects.Where(m => m.MessageType.Contains("ReservationConfirmedV1")).ToListAsync());
        Assert.False(await effects.AnyAsync(m => m.MessageType.Contains("RequestReservationDispositionV1")));
    }

    [QuoteDatabaseFact]
    public async Task ZeroDispositionUsesTheSameObligationInEitherArrivalOrder()
    {
        covered = true;
        await using var services = Services();
        foreach (var receiptFirst in new[] { false, true })
        {
            quoteId = Guid.NewGuid();
            var id = await Prepare(services, Guid.NewGuid());
            await Send(services, new CommitQuoteHoldCommand(id));
            var link = new LinkReservationZeroReceipt(new(id, AccountId!.Value, tenant, site, quoteId,
                Guid.NewGuid(), Guid.NewGuid(), 0, "VND"));
            if (receiptFirst) await Send(services, link);
            using (var action = services.CreateScope())
            {
                var work = action.ServiceProvider.GetRequiredService<SmartParking.OperationService.Application.Common.Interfaces.Persistence.IUnitOfWork>();
                await using var transaction = await work.BeginTransactionAsync(default);
                var row = await action.ServiceProvider.GetRequiredService<IMediator>().Send(new LockReservationBooking(id));
                row!.Status = "CANCELLED";
                row.ActiveClaimSlotId = null;
                action.ServiceProvider.GetRequiredService<ReservationDbContext>().Add(ReservationCancellationEvidence.Record(
                    Guid.NewGuid(), id, row.AccountId, DateTimeOffset.UtcNow, "ADMIN_CORRECTION", null, "{}"));
                await work.SaveChangesAsync();
                await transaction.CommitAsync(default);
            }
            await Send(services, link);
            await Send(services, link);
            using var verify = services.CreateScope();
            var db = verify.ServiceProvider.GetRequiredService<ReservationDbContext>();
            var cancelled = await db.Reservations.SingleAsync(r => r.Id == id);
            Assert.Equal(("CANCELLED", link.Evidence.ReceiptId), (cancelled.Status, cancelled.ZeroReceiptId));
            Assert.Null(cancelled.ActiveClaimSlotId);
            Assert.Single(await db.Set<OutboxMessage>().Where(m => m.MessageType.Contains("RequestReservationDispositionV1") &&
                m.Body.Contains(id.ToString())).ToArrayAsync());
        }
    }

    [QuoteDatabaseFact]
    public async Task CompletedZeroVisitCanLinkWithoutReopeningAnInactiveSite()
    {
        covered = true;
        await using var services = Services();
        var id = await Prepare(services, Guid.NewGuid());
        await Send(services, new CommitQuoteHoldCommand(id));
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();
        db.Add(new StructureSiteStateRow { SiteId = site, TenantId = tenant, Active = false });
        await db.SaveChangesAsync();
        using (var action = services.CreateScope())
        {
            var work = action.ServiceProvider.GetRequiredService<SmartParking.OperationService.Application.Common.Interfaces.Persistence.IUnitOfWork>();
            await using var transaction = await work.BeginTransactionAsync(default);
            var row = await action.ServiceProvider.GetRequiredService<IMediator>().Send(new LockReservationBooking(id));
            row!.EnterParking();
            await Assert.ThrowsAsync<SmartParking.OperationService.Domain.InvalidStructureEditException>(() => work.SaveChangesAsync());
        }
        // Seed a previously completed visit; the financial handler is the operation under test.
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE reservations SET status='COMPLETED',active_claim_slot_id=NULL WHERE id={id}");
        await Send(services, new LinkReservationZeroReceipt(new(id, AccountId!.Value, tenant, site, quoteId, order, receipt, 0, "VND")));
        var linked = await db.Reservations.SingleAsync(r => r.Id == id);
        Assert.Equal(("COMPLETED", receipt), (linked.Status, linked.ZeroReceiptId));
        Assert.Null(linked.ActiveClaimSlotId);
        Assert.False(await db.Set<OutboxMessage>().AnyAsync(m => m.MessageType.Contains("RequestReservationDispositionV1") && m.Body.Contains(id.ToString())));
    }
}
