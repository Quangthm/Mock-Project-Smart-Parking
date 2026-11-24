using MediatR;

namespace SmartParking.OperationService.Application.Usecase.Structure.Commands.CancelMaintenanceWindow;

public sealed record CancelMaintenanceWindowCommand(Guid TenantId, Guid SiteId, Guid WindowId) : IRequest;