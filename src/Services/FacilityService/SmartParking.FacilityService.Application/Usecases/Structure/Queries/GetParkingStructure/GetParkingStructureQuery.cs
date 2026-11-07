using MediatR;
using SmartParking.FacilityService.Application.DTOs;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Queries.GetParkingStructure;

public sealed record GetParkingStructureQuery(Guid SiteId, Guid? TenantId = null) : IRequest<ParkingStructureResponse>;
