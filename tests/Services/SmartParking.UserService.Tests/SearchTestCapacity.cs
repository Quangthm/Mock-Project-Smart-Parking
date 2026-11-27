using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using SmartParking.OperationService.API;
using SmartParking.OperationService.Application.Discovery;
using SmartParking.OperationService.Domain;

namespace SmartParking.UserService.Tests;

internal sealed class SearchTestCapacity(SearchTestClock clock, string mode) : IDiscoveryCapacityReader
{
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool Cancelled;
    public async Task<CarCapacityRead> ReadCarsAsync(Guid tenantId, Guid siteId, IReadOnlyCollection<Guid> slots,
        SmartParking.OperationService.Domain.Reservations.ReservationInterval? stay, CancellationToken ct, IReadOnlySet<Guid>? physicallyOccupied = null)
    {
        ct.ThrowIfCancellationRequested();
        if (mode == "stall")
        {
            Entered.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
            finally { Cancelled = ct.IsCancellationRequested; }
        }
        Assert.NotEqual(Guid.Empty, tenantId);
        return new(clock.Now, mode == "full" ? 0 : stay == null ? Math.Max(0, slots.Count - 1) : slots.Count);
    }
}
