using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using SmartParking.IoTService.Application.Assistant.Management;
namespace SmartParking.IoTService.Persistence;

public sealed partial class AssistantManagementStore(NpgsqlDataSource source) : IAssistantManagementStore, IConnectionTestStore
{
    public bool IsConfigured => true;
    private static string Json<T>(T value) => JsonSerializer.Serialize(value);
    private static ManagementState State(string json) => ManagementRules.Upgrade(JsonSerializer.Deserialize<ManagementState>(json) ?? throw new ManagementUnavailableException());
    public async Task<ManagementState> ReadAsync(CancellationToken ct)
    {
        try { await using var cmd = source.CreateCommand("SELECT state::text FROM assistant_management_state WHERE id=1");
            return State((string?)await cmd.ExecuteScalarAsync(ct) ?? throw new ManagementUnavailableException()); }
        catch (NpgsqlException) { throw new ManagementUnavailableException(); }
    }
    public async Task<string?> CredentialAsync(Guid slot, Guid version, CancellationToken ct)
    {
        try { await using var cmd = source.CreateCommand("SELECT protected_value FROM assistant_management_credentials WHERE slot_id=$1 AND version=$2");
            cmd.Parameters.AddWithValue(slot); cmd.Parameters.AddWithValue(version);
            return (string?)await cmd.ExecuteScalarAsync(ct); }
        catch (NpgsqlException) { throw new ManagementUnavailableException(); }
    }
    public async Task<ManagementState> MutateAsync(Guid actor, AdminMutation input, Func<ManagementState, ManagementWrite> change, CancellationToken ct)
    {
        try
        {
            await using var connection = await source.OpenConnectionAsync(ct);
            await using var tx = await connection.BeginTransactionAsync(ct);
            await using var locked = new NpgsqlCommand("SELECT state::text FROM assistant_management_state WHERE id=1 FOR UPDATE", connection, tx);
            var before = State((string?)await locked.ExecuteScalarAsync(ct) ?? throw new ManagementUnavailableException());
            // Receipt contains a digest only; never persist plaintext input or credentials in audit.
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Json(input))));
            await using var receipt = new NpgsqlCommand("SELECT fingerprint FROM assistant_management_receipts WHERE actor=$1 AND mutation_id=$2", connection, tx);
            receipt.Parameters.AddWithValue(actor); receipt.Parameters.AddWithValue(input.MutationId);
            if (await receipt.ExecuteScalarAsync(ct) is string old)
            {
                if (old != fingerprint) throw new ManagementConflictException(true);
                await tx.CommitAsync(ct); return before;
            }
            if (before.Revision != input.ExpectedRevision) throw new ManagementConflictException(false);
            var write = change(before);
            var state = write.State with { Revision = checked(before.Revision + 1) };
            if (write.Credential is { } credential)
            {
                await using var insert = new NpgsqlCommand("INSERT INTO assistant_management_credentials(slot_id,version,protected_value) VALUES($1,$2,$3)", connection, tx);
                insert.Parameters.AddWithValue(credential.SlotId); insert.Parameters.AddWithValue(credential.Version); insert.Parameters.AddWithValue(credential.ProtectedValue);
                await insert.ExecuteNonQueryAsync(ct);
            }
            if (write.RevokeSlot is { } slot || write.RetireVersion is not null)
            {
                await using var remove = new NpgsqlCommand(write.RevokeSlot is not null
                    ? "DELETE FROM assistant_management_credentials WHERE slot_id=$1" : "DELETE FROM assistant_management_credentials WHERE version=$1", connection, tx);
                remove.Parameters.AddWithValue(write.RevokeSlot ?? write.RetireVersion!.Value); await remove.ExecuteNonQueryAsync(ct);
            }
            await using var update = new NpgsqlCommand("UPDATE assistant_management_state SET state=$1::jsonb WHERE id=1", connection, tx);
            update.Parameters.AddWithValue(Json(state)); await update.ExecuteNonQueryAsync(ct);
            await using var audit = new NpgsqlCommand("INSERT INTO assistant_management_receipts(actor,mutation_id,fingerprint,revision,action,at) VALUES($1,$2,$3,$4,$5,now())", connection, tx);
            audit.Parameters.AddWithValue(actor); audit.Parameters.AddWithValue(input.MutationId); audit.Parameters.AddWithValue(fingerprint);
            audit.Parameters.AddWithValue(state.Revision); audit.Parameters.AddWithValue(input.Action); await audit.ExecuteNonQueryAsync(ct);
            await tx.CommitAsync(ct); return state;
        }
        catch (NpgsqlException) { throw new ManagementUnavailableException(); }
    }
}
