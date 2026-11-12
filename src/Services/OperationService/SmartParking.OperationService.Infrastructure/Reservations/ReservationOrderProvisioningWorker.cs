using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class ReservationOrderProvisioningWorker(IServiceScopeFactory scopes, ILogger<ReservationOrderProvisioningWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var bookings = scope.ServiceProvider.GetRequiredService<IReservationBookingRepository>();
                var now = await bookings.DatabaseNowAsync(stoppingToken);
                var dueOrders = await bookings.DueOrderProvisioningAsync(now, 100, stoppingToken);
                var expiredHolds = await bookings.DueHoldExpiryAsync(now, 100, stoppingToken);
                var zeroPriceAcceptance = await bookings.DueZeroPriceAcceptanceAsync(now, 100, stoppingToken);
                var zeroPriceConfirmations = await bookings.DueNoPaymentConfirmationAsync(now, 100, stoppingToken);
                var paidConfirmations = await bookings.DuePaidConfirmationAsync(now, 100, stoppingToken);

                foreach (var reservationId in dueOrders)
                    await DispatchAsync(reservationId, new ProvisionReservationOrderCommand(reservationId), stoppingToken);
                foreach (var reservationId in expiredHolds)
                    await DispatchAsync(reservationId, new ExpireReservationHoldCommand(reservationId), stoppingToken);
                foreach (var reservationId in zeroPriceAcceptance)
                    await DispatchAsync(reservationId, new AcceptZeroPriceTermsCommand(reservationId), stoppingToken);
                foreach (var reservationId in zeroPriceConfirmations)
                    await DispatchAsync(reservationId, new ConfirmZeroPriceReservationCommand(reservationId), stoppingToken);
                foreach (var reservationId in paidConfirmations)
                {
                    var observed = await bookings.FindOrderProvisioningAsync(reservationId, stoppingToken);
                    if (observed?.CollectionId is { } collectionId)
                        await DispatchAsync(reservationId, new ConfirmPaidReservationCommand(reservationId, collectionId), stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogError(error, "Reservation order/hold recovery sweep failed; durable state is retained.");
            }
        }
    }

    private async Task DispatchAsync(Guid reservationId, IRequest command, CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMediator>().Send(command, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception error)
        {
            logger.LogError(error, "Reservation recovery dispatch failed for {ReservationId}; durable state is retained.",
                reservationId);
        }
    }
}
