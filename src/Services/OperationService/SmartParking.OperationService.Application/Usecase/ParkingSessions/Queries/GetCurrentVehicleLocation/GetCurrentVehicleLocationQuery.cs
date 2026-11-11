using MediatR;

namespace SmartParking.OperationService.Application.Usecase.ParkingSessions.Queries.GetCurrentVehicleLocation;

public sealed record GetCurrentVehicleLocationQuery(Guid AccountId) : IRequest<VehicleLocationResponseDto?>;