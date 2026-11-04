using MediatR;
using SmartParking.OperationService.Domain;

namespace SmartParking.OperationService.Application.Usecase.Structure.Commands.PrepareStructureEdit;

public sealed class PrepareStructureEditCommandHandler(StructureSafetyService service)
    : IRequestHandler<PrepareStructureEditCommand, HoldSnapshot>
{
    public Task<HoldSnapshot> Handle(PrepareStructureEditCommand request, CancellationToken ct) =>
        service.PrepareAsync(request.TenantId, request.SiteId, request.Token, ct);
}
