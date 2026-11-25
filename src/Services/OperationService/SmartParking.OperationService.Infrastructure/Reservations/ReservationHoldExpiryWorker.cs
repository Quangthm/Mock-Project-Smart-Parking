using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class ReservationHoldExpiryWorker(IServiceScopeFactory scopes, ILogger<ReservationHoldExpiryWorker> logger)
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
                var expiredHolds = await bookings.DueHoldExpiryAsync(now, 100, stoppingToken);
                foreach (var reservationId in expiredHolds)
                    await DispatchAsync(reservationId, new ExpireReservationHoldCommand(reservationId), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogError(error, "Reservation hold expiry sweep failed; durable state is retained.");
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
