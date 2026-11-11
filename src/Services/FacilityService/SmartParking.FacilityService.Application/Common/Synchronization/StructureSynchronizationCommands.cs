using MediatR;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;

namespace SmartParking.FacilityService.Application.Common.Synchronization;

public sealed record RecoverStructureSiteCommand(Guid SiteId) : IRequest;
public sealed record PublishStructureOutcomeCommand(StructureSynchronization Synchronization) : IRequest;
public sealed record RecoverPendingStructuresCommand : IRequest<IReadOnlyList<(Guid SiteId, Exception Error)>>;
