using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record ReservationProgress(Guid RequestId, long Version, string AdmissionState,
    ReservationChecks Checks, Guid? ReservationId, string? ReservationState,
    string? OutcomeCode, Guid? OrderId, DateTimeOffset? HoldExpiresAt,
    decimal? BookedAmount, decimal? DepositAmount, string? Currency)
{
    // Several outbox triggers may read the same (or a later) committed snapshot.
    public string EventId => FormattableString.Invariant($"{RequestId:N}/{Version}");

    public static ReservationProgress From(ReservationRequest request, ReservationRow? booking)
    {
        if (booking is not null && (booking.RequestId != request.Id || booking.AccountId != request.AccountId))
            throw new InvalidOperationException("Stored booking ownership is inconsistent.");
        var pending = request.State is "PREPARING" or "PENDING_AUTHORIZATION" or "CHECKING" or "EVALUATING";
        var remaining = pending ? "PENDING" : "SKIPPED";
        var account = request.AccountCheck ?? (request.State == "DENIED" ? "REJECTED" :
            request.DecisionId.HasValue ? "SUCCEEDED" :
            request.State == "FAILED_DEPENDENCY" ? "FAILED_DEPENDENCY" : remaining);
        var plate = request.PlateCheck ?? (request.VehicleBindingState switch
        {
            "ACTIVE" => "SUCCEEDED",
            "INELIGIBLE" => "REJECTED",
            _ => remaining
        });
        var slot = request.SlotCheck ?? (booking is null ? remaining : "SUCCEEDED");
        var evaluation = booking is not null ? "SUCCEEDED" :
            request.State == "FAILED_DEPENDENCY" && plate == "SUCCEEDED" && slot == "SUCCEEDED" ? "FAILED_DEPENDENCY" :
            request.State == "REJECTED" && plate == "SUCCEEDED" && slot == "SUCCEEDED" ? "REJECTED" : remaining;
        return new(request.Id, checked(request.ProgressVersion + (booking?.Version ?? 0)), request.State,
            new(account, plate, slot, evaluation), booking?.Id, booking?.Status,
            booking?.ConfirmationCode ?? request.Code,
            booking?.PaymentOrderId, booking?.HoldExpiresAt, booking?.BookedAmount, booking?.DepositAmount,
            booking?.Currency);
    }
}
