using MediatR;
namespace SmartParking.OperationService.Application.Reservations;

public sealed class ProcessReservationHoldHandler(IReservationBookingRepository bookings, IMediator mediator)
    : IRequestHandler<ProcessReservationHold>
{
    public async Task Handle(ProcessReservationHold command, CancellationToken ct)
    {
        var fact = command.Fact;
        if (fact.State != "PENDING_PAYMENT") return;
        var row = await bookings.FindOrderProvisioningAsync(fact.ReservationId, ct);
        if (row?.Status != "PENDING_PAYMENT" || row.RequestId != fact.WorkflowId) return;
        if (row.DepositAmount > 0) await mediator.Send(new ProvisionReservationOrderCommand(row.Id), ct);
        else if (row.DepositAmount == 0) await mediator.Send(new AcceptZeroPriceTermsCommand(row.Id), ct);
    }
}
