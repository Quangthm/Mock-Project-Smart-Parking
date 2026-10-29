using MediatR;
using SmartParking.ReservationService.Application;
using SmartParking.ReservationService.Application.Common.Models.Exceptions;
using SmartParking.ReservationService.Domain;

namespace SmartParking.ReservationService.Application.Usecase.Structure;

public sealed record PrepareStructureEditCommand(Guid TenantId, Guid SiteId, Guid Token) : IRequest<HoldSnapshot>;
public sealed record ReadStructureImpactQuery(Guid TenantId, Guid SiteId) : IRequest<object>;
public sealed record CompleteStructureEditCommand(
    Guid TenantId,
    Guid SiteId,
    Guid Token,
    EditOutcome? Outcome) : IRequest;
public sealed record ReadStructureSnapshotQuery(Guid TenantId, Guid SiteId) : IRequest<HoldSnapshot>;
public sealed record ForceReleaseStructureHoldsCommand(Guid TenantId, Guid SiteId) : IRequest;

public sealed class PrepareStructureEditCommandHandler(StructureSafetyService service)
    : IRequestHandler<PrepareStructureEditCommand, HoldSnapshot>
{
    public Task<HoldSnapshot> Handle(PrepareStructureEditCommand request, CancellationToken ct) =>
        service.PrepareAsync(request.TenantId, request.SiteId, request.Token, ct);
}

public sealed class ReadStructureImpactQueryHandler(StructureSafetyService service)
    : IRequestHandler<ReadStructureImpactQuery, object>
{
    public Task<object> Handle(ReadStructureImpactQuery request, CancellationToken ct) =>
        service.ImpactAsync(request.TenantId, request.SiteId, ct);
}

public sealed class CompleteStructureEditCommandHandler(StructureSafetyService service)
    : IRequestHandler<CompleteStructureEditCommand>
{
    public Task Handle(CompleteStructureEditCommand request, CancellationToken ct) =>
        service.CompleteAsync(request.TenantId, request.SiteId, request.Token, request.Outcome, ct);
}

public sealed class ReadStructureSnapshotQueryHandler(StructureSafetyService service)
    : IRequestHandler<ReadStructureSnapshotQuery, HoldSnapshot>
{
    public Task<HoldSnapshot> Handle(ReadStructureSnapshotQuery request, CancellationToken ct) =>
        service.ReadSnapshotAsync(request.TenantId, request.SiteId, ct);
}

public sealed class ForceReleaseStructureHoldsCommandHandler(StructureSafetyService service)
    : IRequestHandler<ForceReleaseStructureHoldsCommand>
{
    public Task Handle(ForceReleaseStructureHoldsCommand request, CancellationToken ct) =>
        service.ForceReleaseAsync(request.TenantId, request.SiteId, ct);
}
