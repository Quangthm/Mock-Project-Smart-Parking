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

// This queue is independent of receipt terminal state: completed/expired bookings can still
// owe a durable Facility completion or abort acknowledgement after a remote outage.
public sealed class ReservationFacilityDependencyWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<ReservationFacilityDependencyWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var queryScope = scopes.CreateScope();
                var due = await queryScope.ServiceProvider.GetRequiredService<IReservationFacilityDependencyRepository>()
                    .DueAsync(clock.GetUtcNow(), 100, stoppingToken);
                foreach (var token in due)
                {
                    try
                    {
                        using var scope = scopes.CreateScope();
                        await scope.ServiceProvider.GetRequiredService<IMediator>()
                            .Send(new AdvanceFacilityDependencyCommand(token), stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception error)
                    {
                        logger.LogWarning("Facility dependency retained ({FailureType}).", error.GetType().Name);
                        try
                        {
                            using var retryScope = scopes.CreateScope();
                            await retryScope.ServiceProvider.GetRequiredService<IMediator>()
                                .Send(new ScheduleFacilityDependencyRetryCommand(token), stoppingToken);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                        catch (Exception retryError)
                        { logger.LogWarning("Facility dependency scheduling retained ({FailureType}).", retryError.GetType().Name); }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error)
            { logger.LogWarning("Facility dependency sweep retained ({FailureType}).", error.GetType().Name); }
        }
    }
}
