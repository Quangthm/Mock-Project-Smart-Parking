using MediatR;

namespace SmartParking.FacilityService.Application.Usecases.Occupancy.Commands.ApplyOccupancyProjection;

public sealed class ApplyOccupancyProjectionCommandHandler(IOccupancyProjectionStore store)
    : IRequestHandler<ApplyOccupancyProjectionCommand, bool>
{
    public Task<bool> Handle(ApplyOccupancyProjectionCommand request, CancellationToken cancellationToken) =>
        store.ApplyAsync(request.Projection, cancellationToken);
}