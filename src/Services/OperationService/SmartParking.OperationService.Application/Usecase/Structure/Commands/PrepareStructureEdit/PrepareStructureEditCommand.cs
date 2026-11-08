using MediatR;
using SmartParking.OperationService.Domain;

namespace SmartParking.OperationService.Application.Usecase.Structure.Commands.PrepareStructureEdit;

public sealed record PrepareStructureEditCommand(Guid TenantId, Guid SiteId, Guid Token) : IRequest<HoldSnapshot>;
