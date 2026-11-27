using Quartz;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SmartParking.OperationService.Infrastructure.Reservations;

internal static class ReservationQuartzConfiguration
{
    internal static void AddReservationScheduler(this IServiceCollection services, IConfiguration configuration)
    {
        var connection = configuration.GetConnectionString("Reservation")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:Reservation.");
        services.AddQuartz(q =>
        {
            q.ConfigureScheduler(s =>
            {
                s.InstanceName = "OperationsReservationDeadlines";
                s.InstanceId = "AUTO";
                s.ShutdownJobInterruption = ShutdownJobInterruption.WhenWaitingForJobs;
            });
            q.UsePersistentStore(store =>
            {
                store.UsePostgres(connection);
                store.UseClustering();
                store.UseSystemTextJsonSerializer();
                store.ConfigureStore(s => s.RecoverFiringsCancelledByShutdown = true);
            });
        });
        services.AddQuartzHostedService(o => o.WaitForJobsToComplete = true);
    }
}
