using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Common.Interfaces.Services;

namespace UserService.Persistence.Repositories;

public sealed class AccessEventStore(AppDbContext db, TimeProvider clock) : IAccessEventStore
{
    public void Stage(OperatorAccessChanged message) => db.AccessEventOutbox.Add(new()
    {
        Id = message.EventId, CreatedAt = message.OccurredAt, Payload = JsonSerializer.Serialize(message)
    });

    public async Task<AccessEventLease?> ClaimAsync(CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = clock.GetUtcNow();
        var rows = await db.AccessEventOutbox.FromSqlInterpolated($"""
            SELECT * FROM access_event_outbox WHERE published_at IS NULL
              AND (lease_until IS NULL OR lease_until <= {now})
            ORDER BY created_at, id LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(ct);
        if (rows.Count == 0) return null;
        var row = rows[0];
        row.LeaseId = Guid.NewGuid();
        row.LeaseUntil = now.AddSeconds(30);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new(JsonSerializer.Deserialize<OperatorAccessChanged>(row.Payload)!, row.LeaseId.Value);
    }
    public async Task CompleteAsync(AccessEventLease lease, CancellationToken ct) =>
        await db.AccessEventOutbox.Where(x => x.Id == lease.Message.EventId && x.LeaseId == lease.LeaseId && x.PublishedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.PublishedAt, clock.GetUtcNow())
                .SetProperty(x => x.LeaseId, (Guid?)null).SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null), ct);

    public async Task<bool> ApplyProjectionAsync(OperatorAccessChanged message, CancellationToken ct)
    {
        if (message.SchemaVersion != 1 || message.AggregateVersion < 1 || message.EventId == Guid.Empty)
            throw new InvalidOperationException("Unsupported access event.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO access_event_inbox(id,received_at) VALUES ({message.EventId},{clock.GetUtcNow()})
            ON CONFLICT (id) DO NOTHING
            """, ct);
        if (inserted == 0) { await tx.CommitAsync(ct); return false; }
        var changed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO access_projection(user_id,version,status)
            VALUES ({message.AggregateId},{message.AggregateVersion},{message.Status})
            ON CONFLICT (user_id) DO UPDATE SET version=EXCLUDED.version,status=EXCLUDED.status
            WHERE access_projection.version < EXCLUDED.version
            """, ct);
        await tx.CommitAsync(ct);
        return changed == 1;
    }
}

