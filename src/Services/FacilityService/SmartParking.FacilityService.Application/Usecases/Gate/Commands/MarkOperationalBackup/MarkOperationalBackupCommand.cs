using MediatR;

namespace SmartParking.FacilityService.Application.Usecases.Gate.Commands.MarkOperationalBackup;

public sealed record MarkOperationalBackupCommand(
    Guid SiteId,
    Guid SlotId,
    string Reason) : IRequest;
