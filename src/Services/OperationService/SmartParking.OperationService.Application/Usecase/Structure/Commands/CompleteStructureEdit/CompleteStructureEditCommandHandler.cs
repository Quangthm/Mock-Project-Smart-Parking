using MediatR;
using SmartParking.OperationService.Domain;

namespace SmartParking.OperationService.Application.Usecase.Structure.Commands.CompleteStructureEdit;

public sealed class CompleteStructureEditCommandHandler(StructureSafetyService service)
    : IRequestHandler<CompleteStructureEditCommand>
{
    public Task Handle(CompleteStructureEditCommand request, CancellationToken ct) =>
        service.CompleteAsync(request.TenantId, request.SiteId, request.Token, request.Outcome, ct);
}
