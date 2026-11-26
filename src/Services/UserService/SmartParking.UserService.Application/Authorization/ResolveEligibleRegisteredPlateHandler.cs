using MediatR;
using SmartParking.Contracts.ReservationAdmission;
using SmartParking.UserService.Domain.ValueObjects;
using UserService.Application.Common.Interfaces.Persistence;

namespace UserService.Application.Authorization;

public sealed class ResolveEligibleRegisteredPlateHandler(IVehicleRepository vehicles)
    : IRequestHandler<ResolveEligibleRegisteredPlate, PlateDecisionV1>
{
    public async Task<PlateDecisionV1> Handle(ResolveEligibleRegisteredPlate query, CancellationToken ct)
    {
        var input = query.Request;
        if (input.WorkflowId == Guid.Empty || input.AttemptId == Guid.Empty || input.AccountId == Guid.Empty ||
            string.IsNullOrWhiteSpace(input.Fingerprint) ||
            !LicensePlateNormalizer.TryNormalize(input.CanonicalPlate, "CAR", out var plate) || plate != input.CanonicalPlate)
            throw new ArgumentException("Invalid plate check.");
        // The owning repository uses the unique active-plate index. Deleted history cannot authorize a request.
        var vehicle = await vehicles.FindActiveAsync(plate, ct);
        if (vehicle is null || vehicle.DeletedAt is not null || vehicle.AccountId != input.AccountId ||
            !vehicle.MatchesIdentity(plate, "CAR") || vehicle.BindingRevision <= 0)
            return new(input, false, null, null);
        return new(input, true, "CAR", vehicle.BindingRevision);
    }
}
