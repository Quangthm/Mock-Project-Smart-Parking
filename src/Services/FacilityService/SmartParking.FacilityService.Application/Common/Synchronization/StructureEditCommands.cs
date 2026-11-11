using MediatR;
using SmartParking.FacilityService.Application.Common.Interfaces.Persistence;
using SmartParking.FacilityService.Application.Common.Security;

namespace SmartParking.FacilityService.Application.Common.Synchronization;

public sealed record BeginStructureEditCommand(OwnerScope Owner, Guid SiteId,
    Guid? OperationId = null, Guid? MarkedSlot = null, string? Reason = null) : IRequest<IStructureEditSession>;
public sealed record RecoverAuthorizedStructureCommand(OwnerScope Owner, Guid SiteId) : IRequest;
