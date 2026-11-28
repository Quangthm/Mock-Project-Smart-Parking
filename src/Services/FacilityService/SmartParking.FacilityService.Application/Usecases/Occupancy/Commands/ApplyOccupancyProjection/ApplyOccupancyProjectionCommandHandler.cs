using MediatR;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

namespace SmartParking.FacilityService.Application.Usecases.Occupancy.Commands.ApplyOccupancyProjection;

public sealed class ApplyOccupancyProjectionCommandHandler(IOccupancyProjectionRepository repository, IUnitOfWork work)
    : IRequestHandler<ApplyOccupancyProjectionCommand, bool>
{
    public async Task<bool> Handle(ApplyOccupancyProjectionCommand request, CancellationToken cancellationToken)
    {
        await using var transaction = await work.BeginTransactionAsync(cancellationToken);
        var advanced = await repository.ApplyAsync(request.Projection, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return advanced;
    }
}