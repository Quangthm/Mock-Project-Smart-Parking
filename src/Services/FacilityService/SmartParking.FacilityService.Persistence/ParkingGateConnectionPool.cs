using Npgsql;

namespace SmartParking.FacilityService.Persistence;

// Session gates must not consume the connectors needed to perform SQL queries.
// A separate bounded pool plus non-blocking admission prevents gate owners from
// filling the query pool while all waiting for an additional query connection.
public sealed class ParkingGateConnectionPool : IAsyncDisposable
{
    private readonly NpgsqlDataSource gateSource;
    private readonly SemaphoreSlim admission;

    public ParkingGateConnectionPool(string connectionString, int maximumConnections = 16)
    {
        if (maximumConnections < 2)
            throw new ArgumentOutOfRangeException(nameof(maximumConnections), "An operation needs both an operation gate and a site gate.");

        // NpgsqlDataSource.ConnectionString hides the password by default.
        // Build this source from original configuration, never from a sanitized
        // connection-string property exposed by an already-created data source.
        var settings = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Pooling = true,
            Multiplexing = false,
            NoResetOnClose = false,
            MinPoolSize = 0,
            MaxPoolSize = maximumConnections,
            ApplicationName = "SmartParking.SessionGates"
        };
        gateSource = NpgsqlDataSource.Create(settings.ConnectionString);
        admission = new SemaphoreSlim(maximumConnections, maximumConnections);
    }

    public async Task<ConnectionLease?> TryOpenAsync(CancellationToken cancellationToken)
    {
        if (!await admission.WaitAsync(0, cancellationToken))
            return null;

        try
        {
            var connection = await gateSource.OpenConnectionAsync(cancellationToken);
            return new ConnectionLease(connection, admission);
        }
        catch
        {
            admission.Release();
            throw;
        }
    }

    public sealed class ConnectionLease(NpgsqlConnection connection, SemaphoreSlim admission) : IAsyncDisposable
    {
        private bool disposed;
        public NpgsqlConnection Connection => connection;

        public async ValueTask DisposeAsync()
        {
            if (disposed) return;
            disposed = true;
            try { await connection.DisposeAsync(); }
            finally { admission.Release(); }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await gateSource.DisposeAsync();
        admission.Dispose();
    }
}
