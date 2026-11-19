using MassTransit;
using MediatR;
using SmartParking.Contracts.AccountAccess;
using SmartParking.Contracts.ReservationAdmission;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Persistence.Entities;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class ReservationAdmissionMachine : MassTransitStateMachine<ReservationAdmissionState>
{
    public State Checking { get; private set; } = null!;
    public State Evaluating { get; private set; } = null!;
    public Event<StartQuoteAdmission> Started { get; private set; } = null!;
    public Event<CheckAdmissionDeadline> Deadline { get; private set; } = null!;
    public Event<AccountAccessDecisionV1> Account { get; private set; } = null!;
    public Event<PlateDecisionV1> Plate { get; private set; } = null!;
    public Event<SlotDecisionV1> Slot { get; private set; } = null!;
    public Event<EvaluateQuoteBooking> Evaluate { get; private set; } = null!;

    public ReservationAdmissionMachine()
    {
        InstanceState(x => x.CurrentState);
        Event(() => Started, x => x.CorrelateById(c => c.Message.RequestId));
        Event(() => Deadline, x => x.CorrelateById(c => c.Message.RequestId));
        Event(() => Account, x => x.CorrelateById(c => c.Message.WorkflowId));
        Event(() => Plate, x => x.CorrelateById(c => c.Message.Request.WorkflowId));
        Event(() => Slot, x => x.CorrelateById(c => c.Message.Request.WorkflowId));
        Event(() => Evaluate, x => x.CorrelateById(c => c.Message.RequestId));

        Initially(Apply(When(Started)).IfElse(c => c.Saga.Outcome == "CHECKING",
            next => next.TransitionTo(Checking), next => next.Finalize()));
        During(Checking, Ignore(Started), Branch(Account), Branch(Plate), Branch(Slot), Branch(Deadline));
        During(Evaluating, Ignore(Started), Ignore(Account), Ignore(Plate), Ignore(Slot), Ignore(Deadline),
            Apply(When(Evaluate)).If(c => c.Saga.Outcome != "EVALUATING", next => next.Finalize()));
        During(Final, Ignore(Started), Ignore(Account), Ignore(Plate), Ignore(Slot), Ignore(Deadline), Ignore(Evaluate));
    }

    private EventActivityBinder<ReservationAdmissionState, T> Branch<T>(Event<T> signal) where T : class =>
        Apply(When(signal))
            .If(c => c.Saga.Outcome == "EVALUATING", next => next.TransitionTo(Evaluating))
            .If(c => c.Saga.Outcome is not ("CHECKING" or "EVALUATING"), next => next.Finalize());

    private static EventActivityBinder<ReservationAdmissionState, T> Apply<T>(
        EventActivityBinder<ReservationAdmissionState, T> binder) where T : class =>
        binder.ThenAsync(async context =>
        {
            if (context.CorrelationId != context.Saga.CorrelationId)
                throw new ArgumentException("Admission message correlation mismatch.");
            var mediator = context.GetServiceOrCreateInstance<IMediator>();
            context.Saga.Outcome = await mediator.Send(new ProcessAdmissionSignal(context.Saga.CorrelationId,
                context.Message), context.CancellationToken);
        });
}
