using MediatR;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Layout.Queries.GetOperationalLayout;

public sealed record GetOperationalLayoutQuery(
    Guid SiteId,
    VehicleType? VehicleType,
    string? Floor) : IRequest<OperationalLayoutProjectionDto>;
