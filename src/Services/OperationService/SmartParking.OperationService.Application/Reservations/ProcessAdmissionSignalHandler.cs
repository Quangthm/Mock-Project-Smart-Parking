using System.Text.Json;
using MediatR;
using SmartParking.Contracts.AccountAccess;
using SmartParking.Contracts.ReservationAdmission;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class ProcessAdmissionSignalHandler(IReservationRequestRepository requests, IUnitOfWork work,
    IAccountAccessRequester accounts, IReservationBookingRepository bookings, IAdmissionMessages? messages = null)
    : IRequestHandler<ProcessAdmissionSignal, string>
{
    public async Task<string> Handle(ProcessAdmissionSignal command, CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        var request = await requests.LockAsync(command.RequestId, ct);
        if (request?.AccountCheck is null || request.State is not ("CHECKING" or "EVALUATING")) return "TERMINAL";
        if (messages is null) throw new InvalidOperationException("Reservation Saga messaging is disabled.");
        var now = await bookings.DatabaseNowAsync(ct);
        if (now >= request.OverallDeadline)
            request.ExpireAdmission();
        else if (request.State == "CHECKING")
        {
            var intent = JsonSerializer.Deserialize<QuoteReservationIntent>(request.IntentJson)
                ?? throw new InvalidOperationException("Missing persisted admission intent.");
            var plate = new PlateCheckV1(request.Id, request.Id, request.AccountId, request.Fingerprint, intent.LicensePlate);
            var slot = new SlotCheckV1(request.Id, request.Id, request.AccountId, request.Fingerprint,
                intent.RequestedSlotId, intent.Start, intent.End);
            switch (command.Signal)
            {
                case var _ when slot.Start == default || slot.End <= slot.Start:
                    request.FailQuoteAdmission("FACILITY_RESPONSE_INVALID");
                    break;
                case StartQuoteAdmission:
                    await accounts.RequestAsync(AdmissionMessage.For(request), ct);
                    await messages.CheckPlateAsync(plate, ct);
                    await messages.CheckSlotAsync(slot, ct);
                    break;
                case AccountAccessDecisionV1 decision when decision.WorkflowId == request.Id &&
                    decision.UserId == request.UserId && decision.AccountId == request.AccountId &&
                    decision.Role == "driver" && decision.Operation == request.Operation:
                    if (request.AccountCheck == "PENDING" &&
                        decision.AttemptId == request.AttemptId && decision.AttemptNumber == 1 &&
                        decision.Code != (decision.Allowed ? "ALLOWED" : "CURRENT_ACCESS_DENIED"))
                        request.FailQuoteAdmission("ACCOUNT_RESPONSE_INVALID");
                    else request.AcceptDecision(decision.AttemptId, decision.AttemptNumber, decision.DecisionId,
                        decision.DecidedAt, decision.Allowed, now);
                    break;
                case PlateDecisionV1 decision when decision.Request == plate && request.PlateCheck == "PENDING":
                    if (decision.Eligible ? decision.VehicleType != "CAR" || decision.Revision is not > 0 :
                        decision.VehicleType is not null || decision.Revision is not null)
                        request.FailQuoteAdmission("PLATE_RESPONSE_INVALID");
                    else request.RecordPlate(decision.Eligible, intent.LicensePlate, decision.Revision, now);
                    break;
                case SlotDecisionV1 decision when decision.Request == slot && request.SlotCheck == "PENDING":
                    if (decision.Outcome == "ELIGIBLE" && decision.SiteId is Guid site && site != Guid.Empty &&
                        decision.TenantId is Guid tenant && tenant != Guid.Empty)
                        request.RecordSlot(true, site, tenant);
                    else if (decision.Outcome == "INELIGIBLE" && decision.SiteId is null &&
                        decision.TenantId is null) request.RecordSlot(false, null, null);
                    else request.FailQuoteAdmission(decision.Outcome == "DEPENDENCY_UNAVAILABLE" &&
                        decision.SiteId is null && decision.TenantId is null
                        ? "FACILITY_UNAVAILABLE" : "FACILITY_RESPONSE_INVALID");
                    break;
            }
            if (request.State == "EVALUATING") await messages.RequestEvaluationAsync(request.Id, ct);
        }
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return request.State;
    }
}
