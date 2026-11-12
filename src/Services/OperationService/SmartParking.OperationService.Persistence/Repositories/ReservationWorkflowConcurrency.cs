using Microsoft.EntityFrameworkCore;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Persistence.Repositories;

public sealed class ReservationWorkflowConcurrency(ReservationDbContext db) : IReservationWorkflowConcurrency
{
    public async Task<IAsyncDisposable?> TryEnterAsync(Guid workflowId, CancellationToken ct)
    {
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("Workflow execution must begin outside a database transaction.");
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT pg_try_advisory_lock(hashtextextended(@key, 0))";
            var parameter = command.CreateParameter(); parameter.ParameterName = "key";
            parameter.Value = $"reservation-worker:{workflowId:D}"; command.Parameters.Add(parameter);
            if (await command.ExecuteScalarAsync(ct) is true) return new Lease(db, (string)parameter.Value);
            await db.Database.CloseConnectionAsync();
            return null;
        }
        catch { await db.Database.CloseConnectionAsync(); throw; }
    }

    private sealed class Lease(ReservationDbContext db, string key) : IAsyncDisposable
    {
        private bool disposed;
        public async ValueTask DisposeAsync()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT pg_advisory_unlock(hashtextextended(@key, 0))";
                var parameter = command.CreateParameter(); parameter.ParameterName = "key";
                parameter.Value = key; command.Parameters.Add(parameter);
                await command.ExecuteScalarAsync(CancellationToken.None);
            }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }
}
