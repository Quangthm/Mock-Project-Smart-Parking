using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed class OccupancyOutboxStore(ReservationDbContext db) : IOccupancyOutboxStore
{
    // One atomic statement: select + lease in the same transaction, so the row locks are
    // released as soon as this statement finishes. Callers must not wrap network I/O in it.
    private const string ClaimSql = """
        UPDATE outbox_events
        SET locked_until = now() + (@lease_seconds::double precision * interval '1 second')
        WHERE event_id IN (
            SELECT event_id
            FROM outbox_events
            WHERE published_at IS NULL
              AND (locked_until IS NULL OR locked_until < now())
            ORDER BY occurred_at, sequence_number
            LIMIT @batch_size
            FOR UPDATE SKIP LOCKED
        )
        RETURNING event_id, event_type, occurred_at, sequence_number, site_id, payload
        """;

    private const string MarkPublishedSql = """
        UPDATE outbox_events
        SET published_at = now(), locked_until = NULL
        WHERE event_id = ANY(@event_ids)
        """;

    public async Task<IReadOnlyList<OutboxMessage>> ClaimBatchAsync(
        int batchSize,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        if (batchSize <= 0) return [];

        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ClaimSql;
            AddParameter(command, "lease_seconds", leaseDuration.TotalSeconds);
            AddParameter(command, "batch_size", batchSize);

            var messages = new List<OutboxMessage>(batchSize);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                messages.Add(new OutboxMessage(
                    reader.GetGuid(0),
                    reader.GetString(1),
                    ReadTimestamp(reader, 2),
                    reader.GetInt64(3),
                    reader.GetGuid(4),
                    reader.GetString(5)));
            }

            // RETURNING does not preserve the claim ordering; restore it before publishing.
            return messages
                .OrderBy(message => message.OccurredAt)
                .ThenBy(message => message.SequenceNumber)
                .ToList();
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    public async Task MarkPublishedAsync(
        IReadOnlyCollection<Guid> eventIds,
        CancellationToken cancellationToken)
    {
        if (eventIds.Count == 0) return;

        var connection = db.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose) await connection.OpenAsync(cancellationToken);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = MarkPublishedSql;
            AddParameter(command, "event_ids", eventIds.ToArray());
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            if (shouldClose) await connection.CloseAsync();
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static DateTimeOffset ReadTimestamp(DbDataReader reader, int ordinal)
    {
        var value = reader.GetDateTime(ordinal);
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }
}