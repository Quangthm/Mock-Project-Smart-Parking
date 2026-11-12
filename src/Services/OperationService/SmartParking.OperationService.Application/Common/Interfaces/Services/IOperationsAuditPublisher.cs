using SmartParking.Contracts.Audit;
namespace SmartParking.OperationService.Application.Common.Interfaces.Services;

public interface IOperationsAuditPublisher
{
    // Must enlist in the same scoped DbContext/outbox as the use-case UoW.
    Task PublishAsync(OperationsGateAuditV1 auditEvent, CancellationToken ct);
}
