using Npgsql;
using SmartParking.FacilityService.Application;

namespace SmartParking.FacilityService.Persistence;

// Idempotent projection of OperationService occupancy events onto facility-owned parking_slots.
// Delivery is at-least-once, so replay protection is enforced here (delta design G5):
// processed_events deduplicates by eventId, slot_sequence_watermarks rejects stale sequence
// numbers for the same slot, and occupancy_release_ledger guarantees a session is released
// at most once even if release events are redelivered.
public sealed class OccupancyProjectionRepository(NpgsqlDataSource source) : IOccupancyProjectionRepository
{
    public async Task<bool> ApplyAsync(OccupancyProjectionEvent projectionEvent, CancellationToken ct)
    {
        await using var db = await source.OpenConnectionAsync(ct);
        await using var tx = await db.BeginTransactionAsync(ct);

        // G5 rule 1: a duplicate eventId is recorded once and never re-applied.
        await using (var mark = new NpgsqlCommand("""
            INSERT INTO processed_events (event_id, event_type, processed_at)
            VALUES ($1, $2, now())
            ON CONFLICT (event_id) DO NOTHING
            RETURNING event_id
            """, db, tx))
        {
            mark.Parameters.AddWithValue(projectionEvent.EventId);
            mark.Parameters.AddWithValue(projectionEvent.EventType);
            if (await mark.ExecuteScalarAsync(ct) is null)
            {
                await tx.RollbackAsync(ct);
                return false;
            }
        }

        var applied = false;
        if (projectionEvent.SlotId is { } slot)
        {
            // G5 rules 2-4: apply only when the sequence number advances the slot watermark.
            if (!await IsFreshAsync(db, tx, slot, projectionEvent.SequenceNumber, ct))
            {
                await tx.CommitAsync(ct);
                return false;
            }

            switch (projectionEvent.EventType)
            {
                case "SlotStateChanged":
                    await ApplySlotStateAsync(db, tx, slot, projectionEvent.SiteId, projectionEvent.PhysicalState, ct);
                    break;
                case "OccupancyReleased":
                    await ApplyReleaseAsync(db, tx, projectionEvent.EventId, slot, projectionEvent.SiteId, projectionEvent.OccurredAt, projectionEvent.SessionId, ct);
                    break;
            }

            await AdvanceWatermarkAsync(db, tx, slot, projectionEvent.SequenceNumber, ct);
            applied = true;
        }

        await tx.CommitAsync(ct);
        return applied;
    }

    private static async Task<bool> IsFreshAsync(
        NpgsqlConnection db, NpgsqlTransaction tx, Guid slotId, long sequenceNumber, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand(
            "SELECT last_sequence_number FROM slot_sequence_watermarks WHERE slot_id = $1 FOR UPDATE", db, tx);
        command.Parameters.AddWithValue(slotId);
        var last = await command.ExecuteScalarAsync(ct);
        return last is null || Convert.ToInt64(last) < sequenceNumber;
    }

    private static async Task ApplySlotStateAsync(
        NpgsqlConnection db, NpgsqlTransaction tx, Guid slotId, Guid siteId, string? physicalState, CancellationToken ct)
    {
        var state = string.IsNullOrWhiteSpace(physicalState) ? "OCCUPIED" : physicalState;
        await using var command = new NpgsqlCommand("""
            UPDATE parking_slots
            SET physical_state = $2, updated_at = now()
            WHERE id = $1 AND site_id = $3 AND deleted_at IS NULL
            """, db, tx);
        command.Parameters.AddWithValue(slotId);
        command.Parameters.AddWithValue(state);
        command.Parameters.AddWithValue(siteId);
        await command.ExecuteNonQueryAsync(ct);
    }

    // Release-once (G3): the ledger insert decides whether the slot may be freed. A replay of the
    // same session finds the ledger row already present and the UPDATE matches no rows.
    private static async Task ApplyReleaseAsync(
        NpgsqlConnection db,
        NpgsqlTransaction tx,
        Guid eventId,
        Guid slotId,
        Guid siteId,
        DateTimeOffset occurredAt,
        Guid? sessionId,
        CancellationToken ct)
    {
        if (sessionId is not { } session) return;
        await using var command = new NpgsqlCommand("""
            WITH ledger AS (
                INSERT INTO occupancy_release_ledger (event_id, session_id, slot_id, released_at, processed_at)
                VALUES ($1, $2, $3, $4, now())
                ON CONFLICT (session_id) DO NOTHING
                RETURNING event_id
            )
            UPDATE parking_slots slot
            SET physical_state = 'AVAILABLE', updated_at = now()
            FROM ledger
            WHERE slot.id = $3 AND slot.site_id = $5
              AND slot.physical_state = 'OCCUPIED' AND slot.deleted_at IS NULL
            """, db, tx);
        command.Parameters.AddWithValue(eventId);
        command.Parameters.AddWithValue(session);
        command.Parameters.AddWithValue(slotId);
        command.Parameters.AddWithValue(occurredAt);
        command.Parameters.AddWithValue(siteId);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task AdvanceWatermarkAsync(
        NpgsqlConnection db, NpgsqlTransaction tx, Guid slotId, long sequenceNumber, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO slot_sequence_watermarks (slot_id, last_sequence_number, updated_at)
            VALUES ($1, $2, now())
            ON CONFLICT (slot_id) DO UPDATE
            SET last_sequence_number = EXCLUDED.last_sequence_number, updated_at = now()
            WHERE slot_sequence_watermarks.last_sequence_number < EXCLUDED.last_sequence_number
            """, db, tx);
        command.Parameters.AddWithValue(slotId);
        command.Parameters.AddWithValue(sequenceNumber);
        await command.ExecuteNonQueryAsync(ct);
    }
}
