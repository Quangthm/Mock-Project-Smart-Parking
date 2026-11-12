using MediatR;

namespace SmartParking.FacilityService.Application.Usecases.Occupancy.Commands.ApplyOccupancyProjection;

public sealed class ApplyOccupancyProjectionCommandHandler(IOccupancyProjectionRepository repository)
    : IRequestHandler<ApplyOccupancyProjectionCommand, bool>
{
    public Task<bool> Handle(ApplyOccupancyProjectionCommand request, CancellationToken cancellationToken) =>
        repository.ApplyAsync(request.Projection, cancellationToken);
}