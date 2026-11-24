using MediatR;
using SmartParking.OperationService.Domain;

namespace SmartParking.OperationService.Application.Usecase.Structure.Commands.ApplyMaintenanceWindow;

public sealed record ApplyMaintenanceWindowCommand(
    Guid TenantId,
    Guid SiteId,
    Guid WindowId,
    Guid? UnitId,
    Guid? SlotId,
    IReadOnlyList<Guid>? ScopedUnitIds,
    IReadOnlyList<Guid>? ScopedSlotIds,
    DateTimeOffset Start,
    DateTimeOffset End,
    string Reason) : IRequest<MaintenanceAcknowledgement>;