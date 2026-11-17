using System.Text.Json;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Domain.Entities;

namespace SmartParking.PaymentService.Persistence.Repositories;

public sealed class CheckoutBindingRepository(PaymentDbContext db) : ICheckoutBindingRepository
{
    // Reserve a distinct policy-id namespace; every order code maps to exactly one revision.
    private static Guid PolicyId(long orderCode) => Guid.ParseExact($"cb04973f52fe4d7b{orderCode:x16}", "N");

    public async Task StoreIfAbsentAsync(long orderCode, string protectedBinding, CancellationToken cancellationToken,
        CheckoutBindingProvenance? provenance = null)
    {
        provenance ??= new CheckoutBindingProvenance("CHECKOUT");
        var externalCode = orderCode.ToString(CultureInfo.InvariantCulture);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO checkout_signing_bindings
                (order_code, payment_order_id, protected_binding, source, evidence_reference)
            SELECT {orderCode}, id, {protectedBinding}, {provenance.Source}, {provenance.EvidenceReference}
            FROM payment_orders
            WHERE ({provenance.PaymentOrderId}::uuid IS NOT NULL AND id = {provenance.PaymentOrderId})
               OR ({provenance.PaymentOrderId}::uuid IS NULL AND order_code = {externalCode})
            ON CONFLICT (order_code) DO NOTHING
            """, cancellationToken);
        if (!await db.Set<CheckoutSigningBinding>().AnyAsync(row => row.OrderCode == orderCode &&
            (provenance.PaymentOrderId == null || row.PaymentOrderId == provenance.PaymentOrderId), cancellationToken))
            throw new InvalidOperationException("An existing payment order is required for a checkout binding.");
    }

    public async Task<string?> ReadAsync(long orderCode, CancellationToken cancellationToken)
    {
        var value = await db.Set<CheckoutSigningBinding>().AsNoTracking()
            .Where(row => row.OrderCode == orderCode).Select(row => row.ProtectedBinding)
            .SingleOrDefaultAsync(cancellationToken);
        return value ?? await ReadLegacyAsync(orderCode, cancellationToken);
    }

    // Read-only bridge for exact, already-protected rows from the previous deployment.
    // All new writes go to checkout_signing_bindings. No timestamp/keyring inference.
    public async Task<string?> ReadLegacyAsync(long orderCode, CancellationToken cancellationToken)
    {
        var id = PolicyId(orderCode);
        var payload = await db.PolicyRevisions.AsNoTracking()
            .Where(row => row.Id == id && row.PolicyId == id && row.RevisionNumber == 1 &&
                row.PolicyType == "TARIFF" && row.ScopeType == "GLOBAL")
            .Select(row => row.PolicyValuesJson)
            .SingleOrDefaultAsync(cancellationToken);
        return payload is null ? null : JsonSerializer.Deserialize<string>(payload);
    }
}
