using MediatR;
using SmartParking.OperationService.Domain;

namespace SmartParking.OperationService.Application.Usecase.Structure.Commands.CompleteStructureEdit;

public sealed record CompleteStructureEditCommand(
    Guid TenantId,
    Guid SiteId,
    Guid Token,
    EditOutcome? Outcome) : IRequest;
