using MediatR;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Lots.Queries.ListParkingLots;

public sealed record ListParkingLotsQuery(Guid? TenantId = null) : IRequest<IReadOnlyList<SiteProfile>>;
