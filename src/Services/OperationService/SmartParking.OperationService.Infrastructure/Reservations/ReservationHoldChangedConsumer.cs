using Microsoft.Extensions.DependencyInjection.Extensions;
using MassTransit;
using SmartParking.Contracts.Audit;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartParking.Contracts.AccountAccess;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Persistence;
using SmartParking.OperationService.Infrastructure.Messaging;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class ReservationHoldChangedConsumer(IReservationBookingRepository bookings, IMediator mediator)
    : IConsumer<ReservationHoldChangedV1>
{
    public Task Consume(ConsumeContext<ReservationHoldChangedV1> context) =>
        DispatchAsync(bookings, mediator, context.Message, context.CancellationToken);

    public static async Task DispatchAsync(IReservationBookingRepository bookings, IMediator mediator,
        ReservationHoldChangedV1 message, CancellationToken ct)
    {
        if (message.State != "PENDING_PAYMENT") return;
        var observed = await bookings.FindOrderProvisioningAsync(message.ReservationId, ct);
        if (observed is null || observed.RequestId is null || observed.Status != "PENDING_PAYMENT") return;
        if (observed.DepositAmount > 0m)
        {
            await mediator.Send(new ProvisionReservationOrderCommand(message.ReservationId), ct);
            return;
        }
        if (observed.DepositAmount == 0m)
            await mediator.Send(new AcceptZeroPriceTermsCommand(message.ReservationId), ct);
    }
}
