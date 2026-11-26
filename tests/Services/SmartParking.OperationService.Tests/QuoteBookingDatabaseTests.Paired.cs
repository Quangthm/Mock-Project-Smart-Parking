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
        builder.Logging.AddSimpleConsole().SetMinimumLevel(LogLevel.None).AddFilter("MassTransit", LogLevel.Warning);
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
        builder.Services.AddExceptionHandler<PaymentExceptionHandler>();
        builder.Services.AddProblemDetails();
        await using var host = builder.Build();
        host.UseExceptionHandler();
        host.UseAuthentication();
        host.UseAuthorization();
        host.MapControllers();
        var link = Guid.NewGuid().ToString("N");
        host.MapPost("/v2/payment-requests", () => Results.Json(new { code = "00", data = new {
            checkoutUrl = "https://example.invalid/checkout", qrCode = "test-qr", paymentLinkId = link } }));
        await host.StartAsync();
        settings["PayOs:BaseUrl"] = host.Urls.Single();
        using var http = new HttpClient() { BaseAddress = new(host.Urls.Single()) };
        var signerSettings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
            ["ReservationIntegration:Enabled"] = "true", ["ReservationIntegration:ReviewedSettlementEnabled"] = "true",
            ["ReservationIntegration:AllowHttp"] = "true", ["ReservationIntegration:OperationsPrivateKeyPem"] = rsa.ExportPkcs8PrivateKeyPem()
        }).Build();
        var owner = new ReservationCommercialClient(http, new HttpContextAccessor(), signerSettings);
        await using var operations = Services(owner, listenForPayments: false);
        await using var recovery = Services(owner);
        var runners = operations.GetServices<Microsoft.Extensions.Hosting.IHostedService>().ToList();
        var recoveredBus = recovery.GetRequiredService<IBusControl>();
        var bus = operations.GetRequiredService<IBusControl>();
        using var startup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        foreach (var runner in runners) await runner.StartAsync(startup.Token);
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
                "PAYOS", new(false, "none-v1"), platform, 1, sitePolicy, 1);
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.PostAsJsonAsync("/internal/reservation-quotes/v1", binding)).StatusCode);
            clock.Now = DateTimeOffset.UtcNow.AddMinutes(-10).AddSeconds(15);
            var proof = await owner.IssuePlateQuoteAsync(binding, default);
            quoteId = proof.QuoteId;
            var id = await Prepare(operations, Guid.NewGuid());
            await Send(operations, new CommitQuoteHoldCommand(id));
            using var opScope = operations.CreateScope();
            var opDb = opScope.ServiceProvider.GetRequiredService<ReservationDbContext>();
            async Task<SmartParking.OperationService.Domain.Entities.ReservationRow> Read() =>
                await opDb.Reservations.AsNoTracking().SingleAsync(r => r.Id == id);
            var held = await Read();
            Assert.Equal("HOLD_CREATED", await opDb.ReservationRequests.Where(r => r.Id == id).Select(r => r.State).SingleAsync());
            Assert.Null(held.PaymentOrderId);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));
            while (!await db.PaymentOrders.AnyAsync(o => o.ReferenceId == id)) await Task.Delay(50, timeout.Token);
            var orderId = await db.PaymentOrders.Where(o => o.ReferenceId == id).Select(o => o.Id).SingleAsync();
            Assert.Null((await Read()).PaymentOrderId);
            var lookup = $"/api/v1/payments/orders/{orderId}/reservation";
            var readiness = $"/api/v1/payments/reservations/{id}/checkout-state";
            Assert.Equal(HttpStatusCode.Unauthorized, (await http.GetAsync(lookup)).StatusCode);
            http.DefaultRequestHeaders.Add("Test-Account", Guid.NewGuid().ToString());
            http.DefaultRequestHeaders.Add("X-Actor-AccountId", AccountId.Value.ToString());
            foreach (var path in new[] { lookup, readiness })
            {
                var hidden = (await http.GetFromJsonAsync<JsonElement>(path)).GetProperty("data");
                Assert.Equal("MISSING", hidden.GetProperty("state").GetString());
                Assert.Equal(JsonValueKind.Null, hidden.GetProperty("orderId").ValueKind);
            }
            http.DefaultRequestHeaders.Remove("X-Actor-AccountId");
            http.DefaultRequestHeaders.Remove("Test-Account");
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
            Assert.Empty((await owner.CollectionsAsync(id, orderId, default)).Collections);
            (await Callback(first)).EnsureSuccessStatusCode();
            (await Callback(first)).EnsureSuccessStatusCode();
            Assert.Null((await Read()).PaymentOrderId);
            var reverse = (await http.GetFromJsonAsync<JsonElement>($"/api/v1/payments/orders/{orderId}/reservation")).GetProperty("data");
            Assert.Equal(id, reverse.GetProperty("reservationId").GetGuid());
            Assert.Equal("PAID", reverse.GetProperty("state").GetString());
            var collection = Assert.Single((await owner.CollectionsAsync(id, orderId, default)).Collections);
            Assert.Equal(transactionId, collection.CollectionId);
            await recoveredBus.StartAsync(timeout.Token);
            while ((await Read()).Status != "CONFIRMED") await Task.Delay(50, timeout.Token);
            var confirmed = await Read();
            Assert.Equal(transactionId, confirmed.CollectionId);
            Assert.Equal(orderId, confirmed.PaymentOrderId);
            Assert.NotNull(confirmed.ConfirmationEffectId);
            covered = true;
            start = start.AddHours(4);
            proof = await owner.IssuePlateQuoteAsync(binding with { Start = start, End = start.AddHours(1),
                Coverage = new(true, "covered-1") }, default);
            quoteId = proof.QuoteId;
            id = await Prepare(operations, Guid.NewGuid());
            await Send(operations, new CommitQuoteHoldCommand(id));
            Assert.Equal("CONFIRMED", (await Read()).Status);
            while ((await Read()).ZeroReceiptId == null) await Task.Delay(50, timeout.Token);
            var zero = await Read();
            Assert.Null(zero.HoldExpiresAt);
            Assert.Null(zero.CollectionId);
            Assert.True(await db.Invoices.AnyAsync(i => i.Id == zero.ZeroReceiptId && i.PaymentOrderId == zero.PaymentOrderId));
            Assert.False(await db.PaymentTransactions.AnyAsync(t => t.PaymentOrderId == zero.PaymentOrderId));
        }
        finally
        {
            await recoveredBus.StopAsync();
            foreach (var runner in runners.AsEnumerable().Reverse()) await runner.StopAsync(default);
            await host.StopAsync();
        }
    }
}
