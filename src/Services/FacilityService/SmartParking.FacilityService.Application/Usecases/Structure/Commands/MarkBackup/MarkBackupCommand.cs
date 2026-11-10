using MediatR;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.MarkBackup;

public sealed record MarkBackupCommand(
    Guid SiteId,
    Guid SlotId,
    bool Active,
    Guid? TenantId = null) : IRequest;
