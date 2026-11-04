using MediatR;

namespace SmartParking.OperationService.Application.Usecase.Structure.Commands.ForceReleaseStructureHolds;

public sealed record ForceReleaseStructureHoldsCommand(Guid TenantId, Guid SiteId) : IRequest;
