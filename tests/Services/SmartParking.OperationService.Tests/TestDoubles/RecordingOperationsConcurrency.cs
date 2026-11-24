using SmartParking.OperationService.Application.Common.Interfaces.Persistence;

namespace SmartParking.OperationService.Tests.TestDoubles;

// Records the order in which canonical guards were taken so a test can prove the handler never
// takes the site guard before the account/vehicle guards.
internal sealed class RecordingOperationsConcurrency : IOperationsConcurrency
{
    public List<string> Acquired { get; } = [];

    public Task LockSiteAsync(Guid siteId, CancellationToken ct)
    {
        Acquired.Add("site");
        return Task.CompletedTask;
    }

    public Task LockVehicleAsync(string identity, CancellationToken ct)
    {
        Acquired.Add("vehicle");
        return Task.CompletedTask;
    }
}