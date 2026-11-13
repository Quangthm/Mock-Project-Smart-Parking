using System.Text.Json;
using Npgsql;
using SmartParking.IoTService.Application.Assistant.Management;

namespace SmartParking.IoTService.Persistence;

public sealed partial class AssistantManagementStore
{
    public async Task<TestAdmission?> FindAsync(Guid actor, Guid configuration, long revision, Guid requestId, CancellationToken ct)
    {
        try
        {
            await using var cmd = source.CreateCommand("SELECT configuration_id, revision, model, result::text, admitted_at, statement_timestamp() FROM assistant_connection_tests WHERE actor=$1 AND request_id=$2 AND admitted_at > statement_timestamp() - interval '1 hour'");
            cmd.Parameters.AddWithValue(actor); cmd.Parameters.AddWithValue(requestId);
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;
            if (reader.GetGuid(0) != configuration || reader.GetInt64(1) != revision) throw new ManagementConflictException(true);
            if (!reader.IsDBNull(3)) return new(false, JsonSerializer.Deserialize<ConnectionTestResult>(reader.GetString(3)));
            if (reader.GetDateTime(5) - reader.GetDateTime(4) < ConnectionTestTiming.PendingGrace) return new(false, null);
            return new(false, new(requestId, configuration, revision, reader.GetString(2), "unknown", (long)ConnectionTestTiming.Execution.TotalMilliseconds,
                new DateTimeOffset(reader.GetDateTime(4), TimeSpan.Zero), false, "PROVIDER_TIMEOUT"));
        }
        catch (NpgsqlException) { throw new ManagementUnavailableException(); }
    }

    public async Task<TestAdmission> AdmitAsync(Guid actor, Guid configuration, long revision, Guid requestId,
        string model, CancellationToken ct)
    {
        try
        {
            await using var connection = await source.OpenConnectionAsync(ct);
            await using var tx = await connection.BeginTransactionAsync(ct);
            // Same short lock as every configuration writer; release before external HTTP.
            await using var locked = new NpgsqlCommand("SELECT state::text FROM assistant_management_state WHERE id=1 FOR UPDATE", connection, tx);
            var state = State((string?)await locked.ExecuteScalarAsync(ct) ?? throw new ManagementUnavailableException());
            await using var cleanup = new NpgsqlCommand("DELETE FROM assistant_connection_tests WHERE admitted_at < statement_timestamp() - interval '1 hour'", connection, tx);
            await cleanup.ExecuteNonQueryAsync(ct);
            await using var query = new NpgsqlCommand("SELECT configuration_id, revision, model, result::text, admitted_at, statement_timestamp() FROM assistant_connection_tests WHERE actor=$1 AND request_id=$2", connection, tx);
            query.Parameters.AddWithValue(actor); query.Parameters.AddWithValue(requestId);
            ConnectionTestResult? replay = null;
            var found = false;
            await using (var reader = await query.ExecuteReaderAsync(ct))
            {
                if (await reader.ReadAsync(ct))
                {
                    found = true;
                    if (reader.GetGuid(0) != configuration || reader.GetInt64(1) != revision)
                        throw new ManagementConflictException(true);
                    if (!reader.IsDBNull(3)) replay = JsonSerializer.Deserialize<ConnectionTestResult>(reader.GetString(3));
                    else if (reader.GetDateTime(5) - reader.GetDateTime(4) >= ConnectionTestTiming.PendingGrace)
                        replay = new(requestId, configuration, revision, reader.GetString(2), "unknown", (long)ConnectionTestTiming.Execution.TotalMilliseconds,
                            new DateTimeOffset(reader.GetDateTime(4), TimeSpan.Zero), false, "PROVIDER_TIMEOUT");
                }
            }
            if (found)
            {
                await tx.CommitAsync(ct);
                return new(false, replay is null ? null : replay with { Current = state.Revision == revision && state.Keys.Any(k => k.Id == configuration && k.RevokedAt is null) });
            }
            if (state.Revision != revision) throw new ManagementConflictException(false);
            if (!state.Keys.Any(k => k.Id == configuration && k.RevokedAt is null && k.Model == model))
                throw new ProviderDiagnosticsException(404, "RESOURCE_NOT_FOUND");
            await using var rate = new NpgsqlCommand("SELECT count(*) FROM assistant_connection_tests WHERE actor=$1 AND configuration_id=$2 AND admitted_at > statement_timestamp() - interval '1 minute'", connection, tx);
            rate.Parameters.AddWithValue(actor); rate.Parameters.AddWithValue(configuration);
            if ((long)(await rate.ExecuteScalarAsync(ct))! >= 3) throw new ProviderDiagnosticsException(429, "RATE_LIMITED");
            await using var insert = new NpgsqlCommand("INSERT INTO assistant_connection_tests(actor,request_id,configuration_id,revision,model,admitted_at) VALUES($1,$2,$3,$4,$5,statement_timestamp())", connection, tx);
            insert.Parameters.AddWithValue(actor); insert.Parameters.AddWithValue(requestId); insert.Parameters.AddWithValue(configuration);
            insert.Parameters.AddWithValue(revision); insert.Parameters.AddWithValue(model);
            await insert.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct);
            return new(true, null);
        }
        catch (NpgsqlException) { throw new ManagementUnavailableException(); }
    }

    public async Task CompleteAsync(Guid actor, ConnectionTestResult result, CancellationToken ct)
    {
        try
        {
            await using var cmd = source.CreateCommand("UPDATE assistant_connection_tests SET result=$1::jsonb WHERE actor=$2 AND request_id=$3 AND result IS NULL");
            cmd.Parameters.AddWithValue(JsonSerializer.Serialize(result)); cmd.Parameters.AddWithValue(actor); cmd.Parameters.AddWithValue(result.TestRequestId);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (NpgsqlException) { throw new ManagementUnavailableException(); }
    }
}
