using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using SmartParking.FacilityService.Application.Common.Interfaces.Services;

namespace SmartParking.FacilityService.Persistence;

// Enlists raw Npgsql commands onto ParkingDbContext's active EF transaction when DI provides one
// so state mutations and MassTransit Bus Outbox rows commit or roll back atomically.
internal sealed class FacilityOutboxTransaction : IAsyncDisposable
{
    private readonly ParkingDbContext? _context;
    private readonly NpgsqlConnection? _rawConnection;
    private readonly NpgsqlTransaction? _rawTransaction;
    private readonly IDbContextTransaction? _efTransaction;
    private bool _completed;

    private FacilityOutboxTransaction(
        ParkingDbContext? context,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        NpgsqlConnection? rawConnection,
        NpgsqlTransaction? rawTransaction,
        IDbContextTransaction? efTransaction)
    {
        _context = context;
        Connection = connection;
        Transaction = transaction;
        _rawConnection = rawConnection;
        _rawTransaction = rawTransaction;
        _efTransaction = efTransaction;
    }

    public NpgsqlConnection Connection { get; }
    public NpgsqlTransaction Transaction { get; }

    public static async Task<FacilityOutboxTransaction> BeginAsync(
        NpgsqlDataSource source,
        ParkingDbContext? context,
        CancellationToken ct)
    {
        if (context is not null)
        {
            var efTx = await context.Database.BeginTransactionAsync(ct);
            try
            {
                var conn = (NpgsqlConnection)context.Database.GetDbConnection();
                var npgsqlTx = (NpgsqlTransaction)efTx.GetDbTransaction();
                return new FacilityOutboxTransaction(context, conn, npgsqlTx, null, null, efTx);
            }
            catch
            {
                context.ChangeTracker.Clear();
                await efTx.DisposeAsync();
                throw;
            }
        }

        var rawConn = await source.OpenConnectionAsync(ct);
        try
        {
            var rawTx = await rawConn.BeginTransactionAsync(ct);
            return new FacilityOutboxTransaction(null, rawConn, rawTx, rawConn, rawTx, null);
        }
        catch
        {
            await rawConn.DisposeAsync();
            throw;
        }
    }

    public async Task StageAndCommitAsync(
        ILayoutInvalidationPublisher? publisher,
        Guid tenantId,
        Guid siteId,
        IReadOnlyList<string> resources,
        CancellationToken ct,
        Guid? eventId = null)
    {
        if (publisher is not null && _context is not null)
        {
            if (eventId is { } explicitId)
                await publisher.StageAsync(explicitId, tenantId, siteId, resources, ct);
            else
                await publisher.StageAsync(tenantId, siteId, resources, ct);

            if (_context.ChangeTracker.HasChanges())
                await _context.SaveChangesAsync(ct);
        }

        await CommitCoreAsync(ct);
    }

    public Task CommitWithoutStagingAsync(CancellationToken ct) => CommitCoreAsync(ct);

    public async Task RollbackAsync(CancellationToken ct)
    {
        if (_completed) return;
        _completed = true;
        if (_context is not null) _context.ChangeTracker.Clear();
        if (_efTransaction is not null) await _efTransaction.RollbackAsync(ct);
        else if (_rawTransaction is not null) await _rawTransaction.RollbackAsync(ct);
    }

    private async Task CommitCoreAsync(CancellationToken ct)
    {
        if (_efTransaction is not null) await _efTransaction.CommitAsync(ct);
        else if (_rawTransaction is not null) await _rawTransaction.CommitAsync(ct);
        _completed = true;
    }

    public async ValueTask DisposeAsync()
    {
        if (!_completed && _context is not null)
            _context.ChangeTracker.Clear();

        if (_efTransaction is not null)
        {
            await _efTransaction.DisposeAsync();
            return;
        }

        try
        {
            if (_rawTransaction is not null) await _rawTransaction.DisposeAsync();
        }
        finally
        {
            if (_rawConnection is not null) await _rawConnection.DisposeAsync();
        }
    }
}
