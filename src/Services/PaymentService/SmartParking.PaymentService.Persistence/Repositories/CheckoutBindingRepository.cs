using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;

namespace SmartParking.PaymentService.Persistence.Repositories;

public sealed class CheckoutBindingRepository(PaymentDbContext db) : ICheckoutBindingRepository
{
    // Reserve a distinct policy-id namespace; every order code maps to exactly one revision.
    private static Guid PolicyId(long orderCode) => Guid.ParseExact($"cb04973f52fe4d7b{orderCode:x16}", "N");

    public async Task StoreIfAbsentAsync(long orderCode, string protectedBinding, CancellationToken cancellationToken)
    {
        var id = PolicyId(orderCode);
        var payload = JsonSerializer.Serialize(protectedBinding);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO policy_revisions
                (id, policy_id, policy_type, revision_number, effective_from, scope_type, policy_values, created_by, created_at)
            VALUES ({id}, {id}, 'TARIFF', 1, CURRENT_TIMESTAMP, 'GLOBAL', CAST({payload} AS jsonb), {Guid.Empty}, CURRENT_TIMESTAMP)
            ON CONFLICT (policy_id, revision_number) DO NOTHING
            """, cancellationToken);
    }

    public async Task<string?> ReadAsync(long orderCode, CancellationToken cancellationToken)
    {
        var id = PolicyId(orderCode);
        var payload = await db.PolicyRevisions.AsNoTracking()
            .Where(row => row.PolicyId == id && row.RevisionNumber == 1)
            .Select(row => row.PolicyValuesJson)
            .SingleOrDefaultAsync(cancellationToken);
        return payload is null ? null : JsonSerializer.Deserialize<string>(payload);
    }
}
