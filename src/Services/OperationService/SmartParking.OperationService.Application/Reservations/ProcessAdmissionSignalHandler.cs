using System.Text.Json;
using MediatR;
using SmartParking.Contracts.AccountAccess;
using SmartParking.Contracts.ReservationAdmission;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class ProcessAdmissionSignalHandler(IReservationRequestRepository requests, IUnitOfWork work,
    IAccountAccessRequester accounts, TimeProvider clock, IAdmissionMessages? messages = null)
    : IRequestHandler<ProcessAdmissionSignal, string>
{
    public async Task<string> Handle(ProcessAdmissionSignal command, CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        var request = await requests.LockAsync(command.RequestId, ct);
        if (request?.AccountCheck is null || request.State is not ("CHECKING" or "EVALUATING")) return "TERMINAL";
        if (messages is null) throw new InvalidOperationException("Reservation Saga messaging is disabled.");
        var now = clock.GetUtcNow();
        if (now >= request.OverallDeadline)
            request.FailQuoteAdmission("ADMISSION_TIMED_OUT");
        else if (request.State == "EVALUATING")
        {
            if (command.Signal is EvaluateQuoteBooking)
                request.FailQuoteAdmission("PAYMENT_QUOTE_PROOF_UNAVAILABLE");
        }
        else
        {
            var intent = JsonSerializer.Deserialize<QuoteReservationIntent>(request.IntentJson)
                ?? throw new InvalidOperationException("Missing persisted admission intent.");
            var plate = new PlateCheckV1(request.Id, request.Id, request.AccountId, request.Fingerprint, intent.LicensePlate);
            var slot = new SlotCheckV1(request.Id, request.Id, request.AccountId, request.Fingerprint, intent.RequestedSlotId);
            switch (command.Signal)
            {
                case StartQuoteAdmission:
                    await accounts.RequestAsync(AdmissionMessage.For(request), ct);
                    await messages.CheckPlateAsync(plate, ct);
                    await messages.CheckSlotAsync(slot, ct);
                    break;
                case CheckAdmissionDeadline:
                    if (request.Advance(now)) await accounts.RequestAsync(AdmissionMessage.For(request), ct);
                    break;
                case AccountAccessDecisionV1 decision when decision.WorkflowId == request.Id &&
                    decision.UserId == request.UserId && decision.AccountId == request.AccountId &&
                    decision.Role == "driver" && decision.Operation == request.Operation:
                    if (request.AccountCheck == "PENDING" && request.AwaitingReply && now < request.AttemptDeadline &&
                        decision.AttemptId == request.AttemptId && decision.AttemptNumber == request.AttemptNumber &&
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
                        decision.TenantId is Guid tenant && tenant != Guid.Empty && !string.IsNullOrWhiteSpace(decision.Version))
                        request.RecordSlot(true, site, tenant, decision.Version);
                    else if (decision.Outcome == "INELIGIBLE" && decision.SiteId is null &&
                        decision.TenantId is null && decision.Version is null) request.RecordSlot(false, null, null, null);
                    else request.FailQuoteAdmission(decision.Outcome == "DEPENDENCY_UNAVAILABLE"
                        ? "FACILITY_UNAVAILABLE" : "FACILITY_RESPONSE_INVALID");
                    break;
            }
            if (request.State == "EVALUATING") await messages.EvaluateAsync(request.Id, ct);
        }
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return request.State;
    }
}
