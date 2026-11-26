using SmartParking.Contracts.ReservationPayments;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using SmartParking.PaymentService.Infrastructure.Messaging;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using SmartParking.PaymentService.API.Authentication;
using SmartParking.PaymentService.API.Controllers;
using SmartParking.PaymentService.API.Middleware;
using SmartParking.PaymentService.Application;
using SmartParking.PaymentService.Application.DTOs.Reservations;
using SmartParking.PaymentService.Application.Reservations;
using SmartParking.PaymentService.Application.Usecase.CreatePaymentOrder;
using SmartParking.PaymentService.Application.Usecase.InitiateCheckout;
using SmartParking.PaymentService.Application.Usecase.Reservations.Commands.EnsureReservationDepositOrder;
using SmartParking.PaymentService.Application.Usecase.Reservations.Queries.GetReservationCollections;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Domain.Pricing;
using SmartParking.PaymentService.Persistence;
using SmartParking.PaymentService.Persistence.Repositories;
using SmartParking.PaymentService.Tests.TestDoubles;

namespace SmartParking.PaymentService.Tests.Persistence;

public sealed class ReviewedOrderDatabaseTests
{
    [ReviewedOrderDatabaseFact]
    public async Task SignedIssuanceAndScopedProofUseTheBindingWithoutFacilityVersion()
    {
        using var rsa = RSA.Create(2048);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(ReviewedReservationQuotesController).Assembly.GetName().Name
        });
        builder.WebHost.UseTestServer();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Payment"] = Environment.GetEnvironmentVariable("SMARTPARK_PAYMENT_ORDER_TEST_DB"),
            ["ReservationIntegration:Enabled"] = "true",
            ["ReservationIntegration:ReviewedSettlementEnabled"] = "true",
            ["ReservationIntegration:OperationsPublicKeyPem"] = rsa.ExportRSAPublicKeyPem()
        });
        builder.Services.AddOperationsServiceAuthentication(builder.Configuration);
        builder.Services.AddControllers().AddApplicationPart(typeof(ReviewedReservationQuotesController).Assembly);
        builder.Services.AddApplicationServices();
        builder.Services.AddPersistenceServices(builder.Configuration);
        builder.Services.AddExceptionHandler<PaymentExceptionHandler>();
        builder.Services.AddProblemDetails();
        await using var host = builder.Build();
        host.UseExceptionHandler();
        host.UseAuthentication();
        host.UseAuthorization();
        host.MapControllers();
        await host.StartAsync();

        await using var db = Open();
        var tariff = await db.Tariffs.AsNoTracking().SingleAsync(t => t.Id == Guid.Parse("c0000000-0000-4000-8000-000000000001"));
        tariff.Id = Guid.NewGuid();
        tariff.TenantId = Guid.NewGuid();
        tariff.SiteId = Guid.NewGuid();
        var account = Guid.NewGuid();
        db.Add(tariff);
        db.Add(new PolicyRevision { PolicyId = tariff.Id, PolicyValuesJson = tariff.TariffRulesJson, ScopeId = tariff.SiteId, CreatedBy = account });
        await db.SaveChangesAsync();
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var binding = new ReviewedQuoteBinding(account, "51A12345", tariff.TenantId, tariff.SiteId, Guid.NewGuid(),
            now.AddHours(2), now.AddHours(3), "PAYOS", new(false, "no-cover"), Guid.NewGuid(), 1, Guid.NewGuid(), 1);
        using var http = host.GetTestClient();
        http.BaseAddress = new Uri("https://localhost");
        const string route = "/internal/reservation-quotes/v1";
        using var unsigned = await http.PostAsJsonAsync(route, binding);
        Assert.Equal(HttpStatusCode.Unauthorized, unsigned.StatusCode);
        const string issuer = "SmartParking.OperationService";
        var token = new JwtSecurityToken(issuer, "SmartParking.PaymentService",
            [new Claim("sub", issuer), new Claim("sender", issuer), new Claim("permission", "reservation:quote"),
                new Claim("role", "INTERNAL_SERVICE"), new Claim("scope", "s2s"), new Claim("jti", Guid.NewGuid().ToString("N"))],
            now.UtcDateTime, now.AddMinutes(1).UtcDateTime, new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        using var issued = await http.PostAsJsonAsync(route, binding);
        issued.EnsureSuccessStatusCode();
        var data = (await issued.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("data");
        Assert.False(data.GetProperty("binding").TryGetProperty("facilityVersion", out _));
        var proof = data.Deserialize<ReviewedQuoteProof>(JsonSerializerOptions.Web)!;
        Assert.Equal(binding, proof.Binding);
        var quote = await db.ReservationQuotes.SingleAsync(q => q.Id == proof.QuoteId);
        using var stored = JsonDocument.Parse(quote.ReviewedEvidence!);
        Assert.False(stored.RootElement.GetProperty("Binding").TryGetProperty("FacilityVersion", out _));
        string Read(Guid quoteId, Guid accountId) => $"{route}/{quoteId}?accountId={accountId}";
        Assert.Equal(data.GetRawText(), (await http.GetFromJsonAsync<JsonElement>(Read(proof.QuoteId, account)))
            .GetProperty("data").GetRawText());
        foreach (var invalidProof in new[] { Read(proof.QuoteId, Guid.NewGuid()),
            Read(Guid.NewGuid(), account) })
        {
            using var missing = await http.GetAsync(invalidProof);
            Assert.False(missing.IsSuccessStatusCode);
            Assert.Equal("QUOTE_NOT_FOUND", (await missing.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
        Assert.Null(quote.TermsVersion);
        Assert.False(data.TryGetProperty("quoteVersion", out _));
    }

    [ReviewedOrderDatabaseFact]
    public async Task ReviewedOrderKeepsTheQuoteBindingAndCheckoutEnforcesRuntimeCapability()
    {
        await using var db = Open();
        var payOs = await SeedQuoteAsync(db, "PAYOS", covered: false);
        // Alternate method represents previously persisted commercial evidence, not a gateway or issuance capability.
        var other = await SeedQuoteAsync(db, "MOMO", covered: false);
        var handler = Ensure(db);

        await handler.Handle(new(payOs), default);
        await handler.Handle(new(other), default);
        await handler.Handle(new(payOs), default);
        await handler.Handle(new(other), default);
        var stored = await db.PaymentOrders.AsNoTracking().SingleAsync(o => o.ReferenceId == other.ReservationId);
        var id = stored.Id;
        Assert.Null(stored.TermsVersion);
        Assert.Equal((other.QuoteId, "MOMO", "VND", other.HoldExpiresAt),
            (stored.QuoteId!.Value, stored.ReviewedMethod, stored.ReviewedCurrency, stored.ExpiresAt));
        var changed = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            handler.Handle(new(other with { PaymentMethod = "PAYOS" }), default));
        Assert.Equal("QUOTE_BINDING_CONFLICT", changed.Code);

        await AssertDatabaseError(db, PostgresErrorCodes.CheckViolation, "ck_payment_orders_reviewed_binding",
            $"UPDATE payment_orders SET reviewed_currency = NULL WHERE id = {id}");
        await AssertDatabaseError(db, PostgresErrorCodes.CheckViolation, "ck_payment_orders_reviewed_binding",
            $"UPDATE payment_orders SET order_type = 'PARKING_FEE' WHERE id = {id}");
        await AssertDatabaseError(db, PostgresErrorCodes.CheckViolation, "ck_payment_orders_reviewed_binding",
            $"UPDATE payment_orders SET terms_version = 'legacy' WHERE id = {id}");
        await AssertDatabaseError(db, PostgresErrorCodes.CheckViolation, "ck_payment_orders_zero_disposition",
            $"UPDATE payment_orders SET zero_disposition_reason = 'not zero' WHERE id = {id}");
        await AssertDatabaseError(db, PostgresErrorCodes.UniqueViolation, "idx_reviewed_order_quote",
            $"UPDATE payment_orders SET quote_id = {payOs.QuoteId} WHERE id = {id}");
        await AssertDatabaseError(db, PostgresErrorCodes.UniqueViolation, "idx_reviewed_order_reservation",
            $"UPDATE payment_orders SET reference_id = {payOs.ReservationId} WHERE id = {id}");

        var gateway = new FakePaymentGatewayPort();
        var checkout = new InitiatePaymentCheckoutCommandHandler(new PaymentOrderRepository(db),
            new PaymentTransactionRepository(db), new UnitOfWork(db), gateway,
            new FakeCurrentActor { IsInternalService = true }, NullLogger<InitiatePaymentCheckoutCommandHandler>.Instance);
        var unsupported = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            checkout.Handle(new(id, IdempotencyKey: null, PaymentMethod: PaymentMethod.MOMO), default));
        Assert.Equal("UNSUPPORTED_PAYMENT_METHOD", unsupported.Code);
        var otherCurrency = await SeedQuoteAsync(db, "PAYOS", covered: false, currency: "USD");
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new(otherCurrency), default));
        Assert.Null(gateway.LastInitiateRequest);
        Assert.False(await db.PaymentTransactions.AnyAsync(t => t.PaymentOrderId == id));
    }

    [ReviewedOrderDatabaseFact]
    public async Task FirstPositiveCommandAtTheExactHoldDeadlineFencesTheOrder()
    {
        await using var db = Open();
        var command = await SeedQuoteAsync(db, "PAYOS", covered: false);
        var quotes = new FakeReservationQuoteRepository();
        quotes.Quotes.Add(await db.ReservationQuotes.AsNoTracking().SingleAsync(q => q.Id == command.QuoteId));
        var orders = new FakePaymentOrderRepository();
        var notifications = new RecordingReservationNotifications();
        var handler = new EnsureReservationDepositOrderCommandHandler(orders, quotes,
            new FakeUnitOfWork { DecisionTime = command.HoldExpiresAt }, notifications);
        await handler.Handle(new(command), default);
        await handler.Handle(new(command), default);
        Assert.True(Assert.Single(orders.Orders).IsProvisioningFence);
        Assert.Single(notifications.Hints);
        Assert.Empty(notifications.Receipts);
    }

    [ReviewedOrderDatabaseFact]
    public async Task ExpiredProvisioningHasNoBusinessStatusAndCannotBecomeAnOrder()
    {
        await using var db = Open();
        var active = await SeedQuoteAsync(db, "PAYOS", covered: false);
        var expired = await SeedQuoteAsync(db, "PAYOS", covered: false);
        expired = expired with { HoldExpiresAt = expired.HoldExpiresAt!.Value.AddHours(-1) };
        var handler = Ensure(db);
        await handler.Handle(new(active), default);
        await handler.Handle(new(expired), default);
        await handler.Handle(new(expired), default);
        var resurrect = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            handler.Handle(new(expired with { HoldExpiresAt = active.HoldExpiresAt }), default));
        Assert.Equal("QUOTE_BINDING_CONFLICT", resurrect.Code);

        var orders = new PaymentOrderRepository(db);
        var fence = await db.PaymentOrders.AsNoTracking().SingleAsync(o => o.IdempotencyKey == expired.ReservationId.ToString());
        Assert.True(fence.IsProvisioningFence);
        Assert.Null(fence.Status);
        Assert.Null(await orders.GetByIdAsync(fence.Id));
        Assert.Null(await orders.GetByOrderCodeAsync(fence.OrderCode));
        Assert.Null(await orders.FindReviewedOrderIdAsync(fence.OrderCode, default));
        Assert.True(await orders.HasReviewedReservationAsync(expired.ReservationId, default));
        var create = new CreatePaymentOrderCommandHandler(orders, new UnitOfWork(db));
        var keyConflict = await Assert.ThrowsAsync<PaymentDomainException>(() => create.Handle(new(new(
            fence.TenantId, fence.SiteId, fence.ReferenceId, fence.ReferenceType, fence.FinalAmount,
            fence.AccountId, fence.IdempotencyKey)), default));
        Assert.Equal("FORBIDDEN_RESERVATION_DEPOSIT_CREATION", keyConflict.Code);
        Assert.False(await db.PaymentTransactions.AnyAsync(t => t.PaymentOrderId == fence.Id));
        Assert.False(await db.Invoices.AnyAsync(i => i.PaymentOrderId == fence.Id));
        var collections = new GetReservationCollectionsQueryHandler(orders, new PaymentTransactionRepository(db),
            NullLogger<GetReservationCollectionsQueryHandler>.Instance);
        Assert.Equal("CLOSED_NO_COLLECTION", (await collections.Handle(new(fence.Id), default)).State);
        await AssertDatabaseError(db, PostgresErrorCodes.CheckViolation, "ck_payment_orders_provisioning_fence",
            $"UPDATE payment_orders SET status = 'CANCELLED' WHERE id = {fence.Id}");

        var commercial = await db.PaymentOrders.SingleAsync(o => o.ReferenceId == active.ReservationId);
        Assert.False(commercial.IsProvisioningFence);
        commercial.MarkCancelled(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        Assert.Equal(PaymentOrderStatus.CANCELLED, (await orders.GetByIdAsync(commercial.Id))!.Status);
        Assert.Equal(commercial.Id, await orders.FindReviewedOrderIdAsync(commercial.OrderCode, default));
        await AssertDatabaseError(db, PostgresErrorCodes.CheckViolation, "ck_payment_orders_provisioning_fence",
            $"UPDATE payment_orders SET status = NULL WHERE id = {commercial.Id}");
    }

    [ReviewedOrderDatabaseFact]
    public async Task ZeroValueReceiptDispositionIsProviderIndependentAndIdempotent()
    {
        await using var db = Open();
        var quote = await SeedQuoteAsync(db, "MOMO", covered: true);
        var handler = Ensure(db);
        await using var duplicate = Open();
        await Task.WhenAll(handler.Handle(new(quote), default), Ensure(duplicate).Handle(new(quote), default));
        var id = await db.PaymentOrders.Where(o => o.ReferenceId == quote.ReservationId).Select(o => o.Id).SingleAsync();
        var receiptId = await db.Invoices.Where(i => i.PaymentOrderId == id).Select(i => i.Id).SingleAsync();
        var orders = new PaymentOrderRepository(db);
        var disposition = new RecordZeroDispositionHandler(orders, new UnitOfWork(db));
        var command = new RecordZeroDisposition(quote.ReservationId, quote.ReservationId, quote.AccountId, id, receiptId,
            quote.QuoteId, "VOLUNTARY");
        var mismatch = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            disposition.Handle(command with { ReceiptId = Guid.NewGuid() }, default));
        Assert.Equal("QUOTE_BINDING_CONFLICT", mismatch.Code);

        Assert.Equal(receiptId, await disposition.Handle(command, default));
        Assert.Equal(receiptId, await disposition.Handle(command, default));
        await Assert.ThrowsAsync<PaymentDomainException>(() => disposition.Handle(command with { Reason = "OTHER_REASON" }, default));
        var stored = await db.PaymentOrders.AsNoTracking().SingleAsync(o => o.Id == id);
        Assert.Equal("MOMO", stored.ReviewedMethod);
        Assert.Equal("VOLUNTARY", stored.ZeroDispositionReason);
        Assert.Equal(0, stored.FinalAmount);
        var receipt = await db.Invoices.AsNoTracking().SingleAsync(i => i.Id == receiptId);
        Assert.Equal(id, receipt.PaymentOrderId);
        Assert.Equal(0, receipt.FinalAmount);
        Assert.True(receipt.PricingSnapshot.IsSettled);
        Assert.False(await db.PaymentTransactions.AnyAsync(t => t.PaymentOrderId == id));
        Assert.False(await db.RefundRequests.AnyAsync(r => r.PaymentOrderId == id));

        var gateway = new FakePaymentGatewayPort();
        var checkout = new InitiatePaymentCheckoutCommandHandler(orders, new PaymentTransactionRepository(db),
            new UnitOfWork(db), gateway, new FakeCurrentActor { IsInternalService = true },
            NullLogger<InitiatePaymentCheckoutCommandHandler>.Instance);
        var noCheckout = await Assert.ThrowsAsync<PaymentDomainException>(() =>
            checkout.Handle(new(id, IdempotencyKey: null, PaymentMethod: PaymentMethod.MOMO), default));
        Assert.Equal("ORDER_ALREADY_PAID", noCheckout.Code);
        Assert.Null(gateway.LastInitiateRequest);
    }

    [ReviewedOrderDatabaseFact]
    public async Task LateZeroOrderInvoiceAndReceiptShareTheCommitAndAreIdempotent()
    {
        await using var observer = Open();
        var command = await SeedQuoteAsync(observer, "PAYOS", covered: true);
        Assert.True(command.End < DateTimeOffset.UtcNow);
        var fault = new SettlementCommitFault();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<PaymentDbContext>(options => options.UseNpgsql(
            Environment.GetEnvironmentVariable("SMARTPARK_PAYMENT_ORDER_TEST_DB")).AddInterceptors(fault));
        services.AddMassTransit(registration =>
        {
            registration.AddEntityFrameworkOutbox<PaymentDbContext>(options =>
            {
                options.UsePostgres();
                options.UseBusOutbox(bus => bus.DisableDeliveryService());
            });
            registration.UsingInMemory();
        });
        await using var provider = services.BuildServiceProvider();
        async Task Deliver()
        {
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            var handler = new EnsureReservationDepositOrderCommandHandler(new PaymentOrderRepository(db),
                new ReservationQuoteRepository(db), new UnitOfWork(db),
                new ReservationFinancialNotifications(scope.ServiceProvider.GetRequiredService<ISendEndpointProvider>()));
            await handler.Handle(new(command), default);
        }
        var orders = observer.PaymentOrders.AsNoTracking().Where(o => o.ReferenceId == command.ReservationId);
        var invoices = observer.Invoices.AsNoTracking().Where(i => i.ReferenceId == command.ReservationId);
        var reservation = command.ReservationId.ToString();
        var messages = observer.Set<OutboxMessage>().AsNoTracking().Where(m => m.Body.Contains(reservation));

        fault.Enabled = true;
        await Assert.ThrowsAsync<IOException>(Deliver);
        Assert.False(await orders.AnyAsync());
        Assert.False(await invoices.AnyAsync());
        Assert.False(await messages.AnyAsync());
        fault.Enabled = false;
        await Task.WhenAll(Deliver(), Deliver());
        var order = await orders.SingleAsync();
        var invoice = await invoices.SingleAsync();
        Assert.Null(order.ExpiresAt);
        Assert.Equal(order.Id, invoice.PaymentOrderId);
        Assert.True(invoice.PricingSnapshot.IsSettled);
        using var envelope = JsonDocument.Parse((await messages.SingleAsync()).Body);
        var receipt = envelope.RootElement.GetProperty("message");
        Assert.Equal(invoice.Id, receipt.GetProperty("receiptId").GetGuid());
        Assert.Equal(order.Id, receipt.GetProperty("paymentOrderId").GetGuid());
    }

    private static PaymentDbContext Open() => new(new DbContextOptionsBuilder<PaymentDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("SMARTPARK_PAYMENT_ORDER_TEST_DB")!).Options);

    private static EnsureReservationDepositOrderCommandHandler Ensure(PaymentDbContext db) =>
        new(new PaymentOrderRepository(db), new ReservationQuoteRepository(db), new UnitOfWork(db),
            new RecordingReservationNotifications());

    private static async Task AssertDatabaseError(PaymentDbContext db, string code, string constraint, FormattableString sql)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(sql));
        Assert.Equal(code, error.SqlState);
        Assert.Equal(constraint, error.ConstraintName);
    }

    private static async Task<EnsureReservationOrderV1> SeedQuoteAsync(
        PaymentDbContext db, string method, bool covered, string currency = "VND")
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var binding = new ReviewedQuoteBinding(Guid.NewGuid(), "51A12345", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            covered ? now.AddHours(-2) : now.AddHours(2), covered ? now.AddHours(-1) : now.AddHours(3), method, new QuoteCoverage(covered, "fixture-coverage"),
            Guid.NewGuid(), 1, Guid.NewGuid(), 1);
        var booked = 20_000L;
        var coveredAmount = covered ? booked : 0;
        var pricing = new PricingCalculationResult(currency, binding.Start, binding.End, "Asia/Ho_Chi_Minh", 60, 60, 60, 0,
            false, booked, booked, MonetaryRoundingMode.HALF_UP_WHOLE_VND, Guid.NewGuid(), "Test tariff", 1, []);
        var proof = new ReviewedQuoteProof(Guid.NewGuid(), "ISSUED", covered ? now.AddHours(-3) : now.AddMinutes(5), binding,
            Guid.NewGuid().ToString(), booked, coveredAmount, booked - coveredAmount, currency, "FULL", "Test disclosure", pricing);
        db.ReservationQuotes.Add(new ReservationQuote
        {
            Id = proof.QuoteId, AccountId = binding.AccountId, TenantId = binding.TenantId, SiteId = binding.SiteId,
            RequestedSlotId = binding.RequestedSlotId, StartTime = binding.Start, EndTime = binding.End,
            BookedAmount = proof.BookedAmount, DepositAmount = proof.DepositAmount, Currency = proof.Currency,
            TariffRevisionId = Guid.Parse(proof.TariffVersion), ValidUntil = proof.ValidUntil, ReviewedEvidence = JsonSerializer.Serialize(proof),
            PrepaymentMode = PrepaymentMode.FULL, CoverageVersion = binding.Coverage.Version
        });
        await db.SaveChangesAsync();

        return new(Guid.NewGuid(), binding.AccountId, binding.TenantId, binding.SiteId, proof.QuoteId,
            binding.RequestedSlotId, binding.CanonicalPlate, binding.Start, binding.End, method,
            proof.DepositAmount, proof.Currency, covered, binding.Coverage.Version, covered ? null : now.AddMinutes(10));
    }
}
