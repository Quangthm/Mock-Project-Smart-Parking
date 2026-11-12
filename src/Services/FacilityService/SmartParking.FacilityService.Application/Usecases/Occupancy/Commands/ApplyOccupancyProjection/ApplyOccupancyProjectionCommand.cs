using MediatR;
using SmartParking.FacilityService.Application;

namespace SmartParking.FacilityService.Application.Usecases.Occupancy.Commands.ApplyOccupancyProjection;

public sealed record ApplyOccupancyProjectionCommand(OccupancyProjectionEvent Projection) : IRequest<bool>;