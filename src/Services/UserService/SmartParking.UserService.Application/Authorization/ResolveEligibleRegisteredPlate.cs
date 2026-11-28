using MediatR;
using SmartParking.Contracts.ReservationAdmission;

namespace UserService.Application.Authorization;

public sealed record ResolveEligibleRegisteredPlate(PlateCheckV1 Request) : IRequest<PlateDecisionV1>;
