using Microsoft.AspNetCore.DataProtection;
using SmartParking.PaymentService.Application.Integrations;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Infrastructure.Services;
using SmartParking.PaymentService.Tests.TestDoubles;
using Xunit;

namespace SmartParking.PaymentService.Tests;

public sealed class ImportCheckoutBindingTests
{
    private const long Code = 741852980;

    private static LegacyCheckoutBindingInput Input(string? mode = "SANDBOX", string? key = "synthetic-owner-key",
        string evidence = "owner-export/1", string link = "provider-link", string source = "LEGACY_IMPORT") => new()
        { OrderCode = Code, Mode = mode, Version = 3, ChecksumKey = key, EvidenceReference = evidence,
            ProviderLinkId = link, Source = source };

    private static (ImportCheckoutBindingHandler Handler, CheckoutSigningBindings Bindings) Setup(
        bool withOrder = true, PaymentMethod method = PaymentMethod.PAYOS, bool wrongTenant = false)
    {
        var orders = new FakePaymentOrderRepository();
        var transactions = new FakePaymentTransactionRepository();
        var order = new PaymentOrder { Id = Guid.NewGuid(), TenantId = Guid.NewGuid(), OrderCode = Code.ToString() };
        if (withOrder) orders.Orders.Add(order);
        transactions.Transactions.Add(new PaymentTransaction { Id = Guid.NewGuid(), PaymentOrderId = order.Id,
            TenantId = wrongTenant ? Guid.NewGuid() : order.TenantId, PaymentMethod = method,
            GatewayTransactionId = "provider-link", CheckoutUrl = "https://example.invalid/checkout" });
        var bindings = new CheckoutSigningBindings(new CheckoutBindingFixture(), new EphemeralDataProtectionProvider());
        return (new(orders, transactions, bindings), bindings);
    }

    [Theory]
    [InlineData("FIXTURE", "synthetic-owner-key", "owner-export/1", "LEGACY_IMPORT")]
    [InlineData("LIVE", "", "owner-export/1", "LEGACY_IMPORT")]
    [InlineData("LIVE", "synthetic-owner-key", "", "LEGACY_IMPORT")]
    [InlineData("LIVE", "synthetic-owner-key", "owner-export/1", "CHECKOUT")]
    public async Task InvalidOperatorInput_CannotCreateBinding(string mode, string key, string evidence, string source)
    {
        var (handler, bindings) = Setup();
        var result = await handler.Handle(new(Input(mode, key, evidence, source: source), true), default);
        Assert.Equal("INVALID_INPUT", result.State);
        Assert.Null(await bindings.ReadChecksumKeyAsync(Code, default));
    }

    [Theory]
    [InlineData(false, PaymentMethod.PAYOS, false, "provider-link", "ORDER_NOT_FOUND")]
    [InlineData(true, PaymentMethod.CASH, false, "provider-link", "CHECKOUT_EVIDENCE_MISMATCH")]
    [InlineData(true, PaymentMethod.PAYOS, true, "provider-link", "CHECKOUT_EVIDENCE_MISMATCH")]
    [InlineData(true, PaymentMethod.PAYOS, false, "wrong-link", "CHECKOUT_EVIDENCE_MISMATCH")]
    public async Task MissingOrForeignCheckoutEvidence_CannotCreateBinding(bool withOrder, PaymentMethod method,
        bool wrongTenant, string link, string expected)
    {
        var (handler, bindings) = Setup(withOrder, method, wrongTenant);
        Assert.Equal(expected, (await handler.Handle(new(Input(link: link), true), default)).State);
        Assert.Null(await bindings.ReadChecksumKeyAsync(Code, default));
    }

    [Fact]
    public async Task DryRunDoesNotWrite_ApplyReplays_ConflictingKeyCannotReplaceOriginal()
    {
        var (handler, bindings) = Setup();
        Assert.Equal("READY", (await handler.Handle(new(Input(), false), default)).State);
        Assert.Null(await bindings.ReadChecksumKeyAsync(Code, default));
        Assert.Equal("APPLIED", (await handler.Handle(new(Input(), true), default)).State);
        Assert.Equal("APPLIED", (await handler.Handle(new(Input(), true), default)).State);
        Assert.Equal("CONFLICT", (await handler.Handle(new(Input(key: "synthetic-different-key"), true), default)).State);
        Assert.Equal("synthetic-owner-key", await bindings.ReadChecksumKeyAsync(Code, default));
    }

    [Fact]
    public async Task LegacyMigrationWithoutAuthenticPersistedRow_DoesNotGuessAKey()
    {
        var (handler, bindings) = Setup();
        Assert.Equal("LEGACY_BINDING_NOT_FOUND", (await handler.Handle(new(Input(source: "LEGACY_POLICY"), true), default)).State);
        Assert.Null(await bindings.ReadChecksumKeyAsync(Code, default));
    }
}
