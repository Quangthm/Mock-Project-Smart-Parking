using MediatR;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Application.Usecases.Structure.Commands.ConfigureBackupPolicy;

public sealed record ConfigureBackupPolicyCommand(
    Guid SiteId,
    Guid? UnitId,
    VehicleType? VehicleType,
    int Capacity,
    Guid? TenantId = null) : IRequest;
