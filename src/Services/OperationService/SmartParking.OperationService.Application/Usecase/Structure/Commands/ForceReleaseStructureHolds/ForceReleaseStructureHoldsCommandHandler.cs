using MediatR;

namespace SmartParking.OperationService.Application.Usecase.Structure.Commands.ForceReleaseStructureHolds;

public sealed class ForceReleaseStructureHoldsCommandHandler(StructureSafetyService service)
    : IRequestHandler<ForceReleaseStructureHoldsCommand>
{
    public Task Handle(ForceReleaseStructureHoldsCommand request, CancellationToken ct) =>
        service.ForceReleaseAsync(request.TenantId, request.SiteId, ct);
}
