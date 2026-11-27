using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SmartParking.OperationService.Tests;

internal sealed class ReservationQuotaReadFault : DbCommandInterceptor
{
    public bool Enabled { get; set; }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (Enabled && command.CommandText.Contains("reservation_quota_configs", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Injected reservation quota read failure.");
        return ValueTask.FromResult(result);
    }
}
