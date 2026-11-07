using MediatR;
using SmartParking.OperationService.Domain;

namespace SmartParking.OperationService.Application.Usecase.Structure.Queries.ReadStructureSnapshot;

public sealed record ReadStructureSnapshotQuery(Guid TenantId, Guid SiteId) : IRequest<HoldSnapshot>;
