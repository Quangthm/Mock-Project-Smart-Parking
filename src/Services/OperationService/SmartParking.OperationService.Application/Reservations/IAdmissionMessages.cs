using SmartParking.Contracts.ReservationAdmission;

namespace SmartParking.OperationService.Application.Reservations;

public interface IAdmissionMessages
{
    Task CheckPlateAsync(PlateCheckV1 check, CancellationToken ct);
    Task CheckSlotAsync(SlotCheckV1 check, CancellationToken ct);
    Task RegisterDeadlineAsync(RegisterReservationDeadline deadline, CancellationToken ct);
    Task WakeAsync(Guid requestId, DateTimeOffset deadline, CancellationToken ct);
    Task RequestEvaluationAsync(Guid requestId, CancellationToken ct);
    Task EvaluateAsync(Guid requestId, CancellationToken ct);
}
