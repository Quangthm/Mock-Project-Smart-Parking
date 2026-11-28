using SmartParking.Contracts.Facility;
using SmartParking.FacilityService.Application.Common.Interfaces.Services;

namespace SmartParking.FacilityService.Infrastructure.Messaging;

// Used when the layout outbox is disabled: the local restriction state is still authoritative,
// but no integration event is emitted, so Operations reconciliation simply stays pending.
public sealed class NoOpFacilityCapacityRestrictionPublisher : IFacilityCapacityRestrictionPublisher
{
    public Task StageRestrictedAsync(FacilityCapacityRestrictedV1 message, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task StageReleasedAsync(FacilityCapacityRestrictionReleasedV1 message, CancellationToken ct = default)
        => Task.CompletedTask;
}