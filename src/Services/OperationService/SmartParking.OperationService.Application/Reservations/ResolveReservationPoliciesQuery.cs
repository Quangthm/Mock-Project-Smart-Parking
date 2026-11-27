using MediatR;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

// Internal composition request: callers must first obtain trusted tenant/site facts from Facility.
// No policy-management or arbitrary tenant-selected HTTP endpoint is exposed by this task.
public sealed record ResolveReservationPoliciesQuery(Guid TenantId, Guid SiteId, DateTimeOffset At)
    : IRequest<EffectiveReservationPolicy>;
