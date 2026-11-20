using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using MassTransit;
using RabbitMQ.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Infrastructure.Reservations;
using SmartParking.OperationService.Persistence;
using SmartParking.PaymentService.API.Authentication;
using SmartParking.PaymentService.API.Controllers;
using SmartParking.PaymentService.API.Middleware;
using SmartParking.PaymentService.Infrastructure.Services;
using SmartParking.PaymentService.Persistence;
using SmartParking.PaymentService.Persistence.Outbox;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.OperationService.Tests;

public sealed partial class QuoteBookingDatabaseTests
{
    private static string SettlementSetting(string key) =>
        Environment.GetEnvironmentVariable("SMARTPARK_SETTLEMENT_" + key)
        ?? throw new InvalidOperationException("Missing disposable settlement fixture setting " + key);

    [PairedSettlementFact]
    public async Task SignedQuoteOrderAndVerifiedTransfersReachGuardedConfirmation()
    {
        using var rsa = RSA.Create(2048);
        var paymentFault = new QuoteCommitFault();
        var clock = new AdmissionTestClock(DateTimeOffset.UtcNow);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions {
            ApplicationName = typeof(ReviewedReservationQuotesController).Assembly.GetName().Name });
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.Sources.Clear();
        var settings = builder.Configuration;
        settings.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Payment"] = SettlementSetting("DB"),
            ["RabbitMQ:Host"] = "127.0.0.1", ["RabbitMQ:Port"] = SettlementSetting("PORT"),
            ["RabbitMQ:VirtualHost"] = SettlementSetting("VHOST"), ["RabbitMQ:Username"] = SettlementSetting("PAYMENT_USER"),
            ["RabbitMQ:Password"] = SettlementSetting("PASSWORD"),
            ["ReservationIntegration:Enabled"] = "true", ["ReservationIntegration:ReviewedSettlementEnabled"] = "true",
            ["ReservationIntegration:AllowHttp"] = "true",
            ["ReservationIntegration:OperationsPublicKeyPem"] = rsa.ExportSubjectPublicKeyInfoPem(),
            ["PayOs:ClientId"] = "test-client", ["PayOs:ApiKey"] = "test-key", ["PayOs:ChecksumKey"] = "disposable-checksum"
        });
        builder.Services.AddAuthentication("driver-test")
            .AddScheme<AuthenticationSchemeOptions, AdmissionTestAuthentication>("driver-test", _ => {});
        builder.Services.AddOperationsServiceAuthentication(settings);
        builder.Services.AddControllers().AddApplicationPart(typeof(ReviewedReservationQuotesController).Assembly);
        SmartParking.PaymentService.Application.ApplicationDependencyInjection.AddApplicationServices(builder.Services);
        builder.Services.AddSingleton<TimeProvider>(clock);
        SmartParking.PaymentService.Persistence.PersistenceDependencyInjection.AddPersistenceServices(builder.Services, settings);
        builder.Services.AddDbContext<PaymentDbContext>(o => o.AddInterceptors(paymentFault));
        SmartParking.PaymentService.Infrastructure.InfrastructureDependencyInjection.AddInfrastructureServices(
            builder.Services, settings, x => x.AddPaymentOutbox(settings));
        await using var host = builder.Build();
        host.UseMiddleware<ExceptionHandlingMiddleware>();
        host.UseAuthentication();
        host.UseAuthorization();
        host.MapControllers();
        var link = Guid.NewGuid().ToString("N");
        host.MapPost("/v2/payment-requests", () => Results.Json(new { code = "00", data = new {
            checkoutUrl = "https://example.invalid/checkout", qrCode = "test-qr", paymentLinkId = link } }));
        await host.StartAsync();
        settings["PayOs:BaseUrl"] = host.Urls.Single();
        var transport = new PaymentAcknowledgementFault();
        using var http = new HttpClient(transport) { BaseAddress = new(host.Urls.Single()) };
        var signerSettings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ReservationIntegration:Enabled"] = "true", ["ReservationIntegration:ReviewedSettlementEnabled"] = "true",
            ["ReservationIntegration:AllowHttp"] = "true", ["ReservationIntegration:OperationsPrivateKeyPem"] = rsa.ExportPkcs8PrivateKeyPem()
        }).Build();
        var owner = new ReservationCommercialClient(http, new HttpContextAccessor(), signerSettings);
        await using var operations = Services(owner, listenForPayments: false);
        await using var recovery = Services(owner);
        var recoveredBus = recovery.GetRequiredService<IBusControl>();
        var bus = operations.GetRequiredService<IBusControl>();
        using var startup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        await bus.StartAsync(startup.Token);
        try
        {
            using var paymentScope = host.Services.CreateScope();
            var db = paymentScope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            var tariff = await db.Tariffs.AsNoTracking().SingleAsync(t => t.Id == Guid.Parse("c0000000-0000-4000-8000-000000000001"));
            tariff.Id = Guid.NewGuid(); tariff.TenantId = tenant; tariff.SiteId = site;
            db.Add(tariff);
            var revision = new PolicyRevision { PolicyId = tariff.Id, PolicyValuesJson = tariff.TariffRulesJson,
                ScopeId = site, CreatedBy = AccountId!.Value };
            db.Add(revision);
            await db.SaveChangesAsync();
            var binding = new ReservationQuoteBinding(AccountId.Value, plate, tenant, site, slot, start, Intent.End,
                "PAYOS", new(false, "none-v1"), facilityVersion, platform, 1, sitePolicy, 1);
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsJsonAsync("/internal/reservation-quotes/v1", binding)).StatusCode);
            clock.Now = DateTimeOffset.UtcNow.AddMinutes(-10).AddSeconds(15);
            var proof = await owner.IssuePlateQuoteAsync(binding, default);
            quoteId = proof.QuoteId; quoteVersion = proof.QuoteVersion;
            var id = await Prepare(operations, Guid.NewGuid());
            await Send(operations, new CommitQuoteHoldCommand(id));
            using var opScope = operations.CreateScope();
            var opDb = opScope.ServiceProvider.GetRequiredService<ReservationDbContext>();
            async Task<SmartParking.OperationService.Domain.Entities.ReservationRow> Read() =>
                await opDb.Reservations.AsNoTracking().SingleAsync(r => r.Id == id);
            var held = await Read();
            var delay = proof.ValidUntil - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero) await Task.Delay(delay + TimeSpan.FromMilliseconds(5));
            revision.EffectiveTo = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
            var unavailable = await Assert.ThrowsAsync<SmartParking.OperationService.Application.Common.Models.Exceptions.ReservationGateException>(
                () => owner.IssuePlateQuoteAsync(binding, default));
            Assert.Equal(503, unavailable.StatusCode);
            var recovered = await owner.ReadProofAsync(AccountId.Value, quoteId, quoteVersion, default);
            Assert.Equal(proof, recovered);
            var foreign = await Assert.ThrowsAsync<SmartParking.OperationService.Application.Common.Models.Exceptions.ReservationGateException>(
                () => owner.ReadProofAsync(Guid.NewGuid(), quoteId, quoteVersion, default));
            Assert.Equal("QUOTE_NOT_FOUND", foreign.Code);
            Assert.Equal(foreign.Code, (await Assert.ThrowsAsync<SmartParking.OperationService.Application.Common.Models.Exceptions.ReservationGateException>(
                () => owner.ReadProofAsync(AccountId.Value, Guid.NewGuid(), quoteVersion, default))).Code);
            var trigger = await opDb.Set<MassTransit.EntityFrameworkCoreIntegration.OutboxMessage>()
                .SingleAsync(m => m.MessageType.Contains("ReservationHoldChangedV1") && m.Body.Contains(id.ToString()));
            using var envelope = JsonDocument.Parse(trigger.Body);
            var hold = envelope.RootElement.GetProperty("message").Deserialize<ReservationHoldChangedV1>(JsonSerializerOptions.Web)!;
            await bus.Publish(hold);
            using var holdTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            while ((await Read()).PaymentOrderId == null) await Task.Delay(100, holdTimeout.Token);
            var row = await Read();
            var orderId = row.PaymentOrderId!.Value;
            var request = new DepositOrderRequest(id, row.Id, AccountId.Value, tenant, site, quoteId, quoteVersion,
                proof.DepositAmount, "VND", "PAYOS", held.HoldExpiresAt!.Value);
            Assert.Equal(orderId, (await owner.EnsureOrderAsync(request, default)).OrderId);
            await AssertConflict(() => owner.EnsureOrderAsync(request with { PaymentMethod = "CASH" }, default));
            Assert.Equal("ACCEPTED", (await owner.ReadProofAsync(AccountId.Value, quoteId, quoteVersion, default)).Status);
            http.DefaultRequestHeaders.Add("Test-Account", AccountId.Value.ToString());
            var checkout = await http.PostAsJsonAsync($"/api/v1/payments/orders/{orderId}/checkout", new {});
            checkout.EnsureSuccessStatusCode();
            var checkoutData = (await checkout.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
            Assert.Equal("test-qr", checkoutData.GetProperty("qrCode").GetString());
            var orderCode = checkoutData.GetProperty("orderCode").GetInt64();
            var transactionId = checkoutData.GetProperty("paymentTransactionId").GetGuid();
            var transferTimes = new Dictionary<string, string>();
            async Task<HttpResponseMessage> Callback(string reference)
            {
                transferTimes.TryAdd(reference, DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).ToString("yyyy-MM-dd HH:mm:ss"));
                var data = new Dictionary<string, object?> { ["orderCode"] = orderCode, ["amount"] = (long)proof.DepositAmount,
                    ["currency"] = "VND", ["code"] = "00", ["paymentLinkId"] = link, ["reference"] = reference,
                    ["transactionDateTime"] = transferTimes[reference] };
                return await http.PostAsJsonAsync("/api/v1/payments/payos/webhook", new { code = "00", data,
                    signature = PayOsSignatureService.CreateSignatureFromDictionary(data, "disposable-checksum") });
            }
            // Stage the production financial subscription before the consumer comes online.
            const string queue = "smartpark-operation-reservation-payment-succeeded-v1";
            const string exchange = "smartpark.payment.payment-succeeded.v1";
            var factory = new ConnectionFactory { HostName = "127.0.0.1", Port = int.Parse(SettlementSetting("PORT")),
                VirtualHost = SettlementSetting("VHOST"), UserName = SettlementSetting("OPERATIONS_USER"),
                Password = SettlementSetting("PASSWORD") };
            await using var connection = await factory.CreateConnectionAsync();
            await using var delivery = await connection.CreateChannelAsync();
            await delivery.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: true);
            await delivery.ExchangeDeclareAsync(queue, ExchangeType.Fanout, durable: true);
            await delivery.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false);
            await delivery.QueueBindAsync(queue, queue, "");
            await delivery.ExchangeBindAsync(queue, exchange, "");

            var first = Guid.NewGuid().ToString("N");
            paymentFault.Enabled = true;
            Assert.Equal(HttpStatusCode.InternalServerError, (await Callback(first)).StatusCode);
            paymentFault.Enabled = false;
            Assert.Empty(await owner.ReadCollectionsAsync(orderId, default));
            Assert.False(await db.Set<MassTransit.EntityFrameworkCoreIntegration.OutboxMessage>().AnyAsync(m => m.Body.Contains(orderId.ToString())));
            transport.FailNextLookup = true;
            (await Callback(first)).EnsureSuccessStatusCode();
            (await Callback(first)).EnsureSuccessStatusCode();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
            BasicGetResult? original;
            while ((original = await delivery.BasicGetAsync(queue, autoAck: false, timeout.Token)) == null)
                await Task.Delay(100, timeout.Token);
            using var originalEnvelope = JsonDocument.Parse(original.Body);
            Assert.Equal(transactionId, originalEnvelope.RootElement.GetProperty("message").GetProperty("paymentTransactionId").GetGuid());
            // Closing an unacknowledged channel must return that exact delivery to the durable queue.
            await delivery.CloseAsync();
            await using var retried = await connection.CreateChannelAsync();
            var redelivered = await retried.BasicGetAsync(queue, autoAck: false, timeout.Token);
            Assert.NotNull(redelivered);
            Assert.True(redelivered.Redelivered);
            Assert.Equal(original.BasicProperties.MessageId, redelivered.BasicProperties.MessageId);
            Assert.True(original.Body.Span.SequenceEqual(redelivered.Body.Span));
            await retried.BasicNackAsync(redelivered.DeliveryTag, multiple: false, requeue: true, timeout.Token);
            await recoveredBus.StartAsync(timeout.Token);
            async Task AwaitConfirmationToken(Guid reservationId, CancellationToken ct)
            {
                while (!await opDb.Set<SmartParking.OperationService.Domain.Reservations.ReservationFacilityDependency>()
                    .AsNoTracking().AnyAsync(t => t.WorkflowId == reservationId && t.Purpose == "CONFIRMATION", ct))
                    await Task.Delay(100, ct);
            }
            await AwaitConfirmationToken(id, timeout.Token);
            await HoldToken(operations, id, "CONFIRMATION");
            await Send(operations, new ConfirmPaidReservationCommand(id, transactionId));
            Assert.Equal("CONFIRMED", (await Read()).Status);
            Assert.Equal(1, transport.FailedLookups);
            var evidence = Assert.Single(await owner.ReadCollectionsAsync(orderId, default));
            Assert.Equal((transactionId, first), (evidence.CollectionId, evidence.ProviderReference));
            (await Callback(Guid.NewGuid().ToString("N"))).EnsureSuccessStatusCode();
            while (!await db.RefundRequests.AsNoTracking().AnyAsync(r => r.PaymentOrderId == orderId)) await Task.Delay(100, timeout.Token);
            var additional = Assert.Single(await owner.ReadCollectionsAsync(orderId, default), c => c.CollectionId != transactionId);
            await owner.ReconcileAsync(id, additional.Collection(), "COLLECTION_BINDING_CONFLICT", default);
            Assert.Equal(1, await db.RefundRequests.CountAsync(r => r.PaymentOrderId == orderId));
            Assert.Equal(transactionId, (await Read()).CollectionId);
            await VerifyZeroAndCompetition(owner, db, binding, revision, clock);

            // Create two further non-overlapping holds through the actual guarded join.
            async Task<Guid> NextHold(bool isCovered)
            {
                covered = isCovered;
                start = start.AddHours(3);
                var next = binding with { Start = start, End = Intent.End,
                    Coverage = new(isCovered, isCovered ? "covered-1" : "none-v1") };
                proof = await owner.IssuePlateQuoteAsync(next, default);
                quoteId = proof.QuoteId;
                quoteVersion = proof.QuoteVersion;
                var nextId = await Prepare(operations, Guid.NewGuid());
                await Send(operations, new CommitQuoteHoldCommand(nextId));
                return nextId;
            }
            id = await NextHold(false);
            var paidId = id;
            await Send(operations, new ProvisionReservationOrderCommand(id));
            var latePaid = await Read();
            link = Guid.NewGuid().ToString("N");
            var lateCheckout = await http.PostAsJsonAsync($"/api/v1/payments/orders/{latePaid.PaymentOrderId}/checkout", new {});
            lateCheckout.EnsureSuccessStatusCode();
            var lateData = (await lateCheckout.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
            orderCode = lateData.GetProperty("orderCode").GetInt64();
            transactionId = lateData.GetProperty("paymentTransactionId").GetGuid();
            var lateProof = proof;

            id = await NextHold(true);
            transport.LoseNextOrderResponse = true;
            await Send(operations, new AcceptZeroPriceTermsCommand(id));
            var zeroRow = await Read();
            Assert.Equal("UNKNOWN", zeroRow.ZeroPriceAcceptanceState);
            var zeroOrder = await db.PaymentOrders.AsNoTracking().SingleAsync(o => o.ReferenceId == id);
            var zeroReceipt = await db.Invoices.AsNoTracking().SingleAsync(i => i.PaymentOrderId == zeroOrder.Id);
            var untilH = zeroRow.HoldExpiresAt!.Value - DateTimeOffset.UtcNow;
            if (untilH > TimeSpan.Zero) await Task.Delay(untilH + TimeSpan.FromMilliseconds(20));
            await Send(operations, new ExpireReservationHoldCommand(id));
            await Send(operations, new AcceptZeroPriceTermsCommand(id));
            await Send(operations, new ConfirmZeroPriceReservationCommand(id));
            Assert.Equal(("EXPIRED", "NO_ENTITLEMENT", zeroReceipt.Id), ((await Read()).Status,
                (await Read()).ConfirmationState, (await Read()).ZeroReceiptId));
            Assert.False(await db.PaymentTransactions.AnyAsync(t => t.PaymentOrderId == zeroOrder.Id));
            Assert.NotNull((await db.PaymentOrders.AsNoTracking().SingleAsync(o => o.Id == zeroOrder.Id)).ZeroDispositionReason);

            id = paidId;
            await Send(operations, new ExpireReservationHoldCommand(id));
            Assert.Equal("EXPIRED", (await Read()).Status);
            proof = lateProof;
            (await Callback(Guid.NewGuid().ToString("N"))).EnsureSuccessStatusCode();
            using var lateTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            await AwaitConfirmationToken(id, lateTimeout.Token);
            await HoldToken(operations, id, "CONFIRMATION");
            await Send(operations, new ConfirmPaidReservationCommand(id, transactionId));
            var reacquired = await Read();
            Assert.Equal("CONFIRMED", reacquired.Status);
            Assert.True(reacquired.ConfirmationEvaluatedAt >= reacquired.HoldExpiresAt &&
                reacquired.ConfirmationEvaluatedAt < reacquired.ExpectedStartTime);

            id = await NextHold(true);
            await Send(operations, new AcceptZeroPriceTermsCommand(id));
            await Send(operations, new ConfirmZeroPriceReservationCommand(id));
            await HoldToken(operations, id, "CONFIRMATION");
            await Send(operations, new ConfirmZeroPriceReservationCommand(id));
            var zeroConfirmed = await Read();
            Assert.Equal("CONFIRMED", zeroConfirmed.Status);
            Assert.True(zeroConfirmed.ConfirmationEvaluatedAt < zeroConfirmed.HoldExpiresAt);
            Assert.True(await db.Invoices.AnyAsync(i => i.Id == zeroConfirmed.ZeroReceiptId && i.PaymentOrderId == zeroConfirmed.PaymentOrderId));
            var zeroCheckout = await http.PostAsJsonAsync($"/api/v1/payments/orders/{zeroConfirmed.PaymentOrderId}/checkout", new {});
            Assert.Equal(HttpStatusCode.Conflict, zeroCheckout.StatusCode);
            Assert.False(await db.PaymentTransactions.AnyAsync(t => t.PaymentOrderId == zeroConfirmed.PaymentOrderId));
            Assert.Equal("NO_VERIFIED_COLLECTION", (await owner.CollectionsAsync(id, zeroConfirmed.PaymentOrderId!.Value, default)).State);

            // Historical vehicle-based review still provisions on the signed legacy PUT.
            var legacy = await http.PostAsJsonAsync("/api/v1/pricing/reservations/quote", new {
                siteId = site, requestedSlotId = slot, start, end = start.AddHours(1), vehicleId = Guid.NewGuid(),
                paymentMethod = "PAYOS", tenantId = tenant, accountId = AccountId });
            legacy.EnsureSuccessStatusCode();
            var old = (await legacy.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
            var historical = new DepositOrderRequest(Guid.NewGuid(), Guid.NewGuid(), AccountId.Value, tenant, site,
                old.GetProperty("quoteId").GetGuid(), old.GetProperty("version").GetString()!,
                old.GetProperty("depositAmount").GetDecimal(), "VND", "PAYOS",
                DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.AddMinutes(5).ToUnixTimeSeconds()));
            var legacyOrder = await owner.EnsureOrderAsync(historical, default);
            Assert.Equal("CREATED", legacyOrder.State);
            Assert.Equal(legacyOrder, await owner.EnsureOrderAsync(historical, default));
        }
        finally { await recoveredBus.StopAsync(); await bus.StopAsync(); await host.StopAsync(); }
    }

    private static async Task AssertConflict(Func<Task> action)
    {
        var error = await Assert.ThrowsAsync<SmartParking.OperationService.Application.Common.Models.Exceptions.ReservationGateException>(action);
        Assert.Equal((409, "QUOTE_BINDING_CONFLICT"), (error.StatusCode, error.Code));
    }

    private static async Task VerifyZeroAndCompetition(ReservationCommercialClient owner, PaymentDbContext db,
        ReservationQuoteBinding binding, PolicyRevision revision, AdmissionTestClock clock)
    {
        clock.Now = DateTimeOffset.UtcNow;
        revision.EffectiveTo = null;
        await db.SaveChangesAsync();
        var paid = await owner.IssuePlateQuoteAsync(binding, default);
        var h = DateTimeOffset.FromUnixTimeSeconds(clock.Now.AddSeconds(4).ToUnixTimeSeconds());
        DepositOrderRequest Request() => new(Guid.NewGuid(), Guid.NewGuid(), binding.AccountId, binding.TenantId,
            binding.SiteId, paid.QuoteId, paid.QuoteVersion, paid.DepositAmount, paid.Currency, binding.PaymentMethod, h);
        var a = Request();
        var b = Request();
        var contenders = await Task.WhenAll(owner.EnsureOrderAsync(a, default), owner.EnsureOrderAsync(b, default));
        var winner = Assert.Single(contenders, r => r.State == "CREATED");
        Assert.Single(contenders, r => r.State == "CLOSED_NO_ORDER");
        var zero = await owner.IssuePlateQuoteAsync(binding with { Coverage = new(true, "test-entitlement") }, default);
        var z = new ZeroOrderBinding(Guid.NewGuid(), Guid.NewGuid(), binding.AccountId, binding.TenantId, binding.SiteId,
            binding.RequestedSlotId, binding.CanonicalPlate, binding.Start, binding.End, binding.PaymentMethod,
            zero.QuoteId, zero.QuoteVersion, true, "test-entitlement", "VND", 0, h);
        var accepted = await owner.AcceptZeroAsync(z, default);
        Assert.Equal("ACCEPTED", accepted.State);
        await AssertConflict(() => owner.AcceptZeroAsync(z with { CoverageVersion = "different" }, default));
        Assert.True(await db.Invoices.AnyAsync(i => i.PaymentOrderId == accepted.OrderId && i.Id == accepted.ReceiptId && i.FinalAmount == 0));
        Assert.False(await db.PaymentTransactions.AnyAsync(t => t.PaymentOrderId == accepted.OrderId));
        var wait = h - DateTimeOffset.UtcNow;
        if (wait > TimeSpan.Zero) await Task.Delay(wait + TimeSpan.FromMilliseconds(10));
        Assert.Equal(winner.OrderId, (await owner.EnsureOrderAsync(winner.AcceptedRequest, default)).OrderId);
        Assert.Equal(accepted, await owner.AcceptZeroAsync(z, default));
        var expired = await owner.IssuePlateQuoteAsync(binding, default);
        var fenced = Request() with { QuoteId = expired.QuoteId, QuoteVersion = expired.QuoteVersion };
        Assert.Equal("CLOSED_NO_ORDER", (await owner.EnsureOrderAsync(fenced, default)).State);
        Assert.Equal("CLOSED_NO_ORDER", (await owner.EnsureOrderAsync(fenced, default)).State);
        await AssertConflict(() => owner.EnsureOrderAsync(fenced with { ExpiresAt = h.AddMinutes(2) }, default));
        var evidence = new ZeroOrderEvidence(z, accepted.OrderId!.Value, accepted.ReceiptId!.Value);
        await owner.ReconcileZeroAsync(evidence, "PAYMENT_HOLD_EXPIRED", default);
        await owner.ReconcileZeroAsync(evidence, "PAYMENT_HOLD_EXPIRED", default);
        Assert.Equal("PAYMENT_HOLD_EXPIRED", (await db.PaymentOrders.AsNoTracking().SingleAsync(o => o.Id == accepted.OrderId)).ZeroDispositionReason);
        Assert.False(await db.RefundRequests.AnyAsync(r => r.PaymentOrderId == accepted.OrderId));
    }
}
