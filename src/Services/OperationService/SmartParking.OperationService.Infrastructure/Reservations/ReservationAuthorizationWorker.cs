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

public sealed class ReservationAuthorizationWorker(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<ReservationAuthorizationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var includeSaga = scope.ServiceProvider.GetService<IAdmissionMessages>() is not null;
                var due = await scope.ServiceProvider.GetRequiredService<IReservationRequestRepository>()
                    .DueAsync(clock.GetUtcNow(), includeSaga, stoppingToken);
                foreach (var id in due)
                {
                    using var requestScope = scopes.CreateScope();
                    try
                    {
                        var mediator = requestScope.ServiceProvider.GetRequiredService<IMediator>();
                        var request = await requestScope.ServiceProvider.GetRequiredService<IReservationRequestRepository>()
                            .FindForWorkerAsync(id, stoppingToken);
                        if (request?.State == "EVALUATING")
                            await mediator.Send(new CommitQuoteHoldCommand(id), stoppingToken);
                        else
                        {
                            await mediator.Send(new AdvanceReservationAuthorizationCommand(id), stoppingToken);
                            await mediator.Send(new ProgressReservationCommand(id), stoppingToken);
                        }
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception error)
                    {
                        logger.LogWarning("Reservation work retained ({FailureType}).", error.GetType().Name);
                        using var recoveryScope = scopes.CreateScope();
                        try
                        {
                            await recoveryScope.ServiceProvider.GetRequiredService<IMediator>()
                                .Send(new RecordReservationWorkFailureCommand(id), stoppingToken);
                        }
                        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                        catch (Exception recoveryError)
                        { logger.LogWarning("Reservation recovery scheduling retained ({FailureType}).", recoveryError.GetType().Name); }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error)
            {
                // No raw SQL/connection/provider values in this background failure log.
                logger.LogWarning("Reservation authorization sweep failed ({FailureType}); pending state is retained.", error.GetType().Name);
            }
        }
    }
}
