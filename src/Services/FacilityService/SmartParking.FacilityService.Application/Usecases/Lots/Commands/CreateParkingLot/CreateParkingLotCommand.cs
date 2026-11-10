using MediatR;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Lots.Commands.CreateParkingLot;

public sealed record CreateParkingLotCommand(
    string Code,
    string Name,
    string Address,
    decimal? Latitude = null,
    decimal? Longitude = null,
    Guid? TenantId = null) : IRequest<SiteProfile>;
