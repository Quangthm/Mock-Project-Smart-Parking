using MediatR;

namespace SmartParking.FacilityService.Application.Usecases.Gate.Commands.ReleaseOperationalBackup;

public sealed record ReleaseOperationalBackupCommand(
    Guid SiteId,
    Guid SlotId) : IRequest;
