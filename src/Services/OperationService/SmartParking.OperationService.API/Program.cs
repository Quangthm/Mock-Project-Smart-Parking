using Quartz;
using SmartParking.OperationService.Infrastructure.Reservations;

namespace SmartParking.OperationService.API;

public static class Program
{
    public static async Task Main(string[] args)
    {
        if (args.FirstOrDefault() != "redrive-reservation-deadline")
        {
            await OperationHost.Build(args).RunAsync();
            return;
        }
        if (args.Length != 2) throw new ArgumentException("Supply the exact reservation deadline job name.");
        await using var host = OperationHost.Build([]);
        var scheduler = await host.Services.GetRequiredService<ISchedulerFactory>().GetScheduler();
        try
        {
            var key = new JobKey(args[1], ReservationDeadlineJob.Group);
            var job = await scheduler.GetJobDetail(key) ?? throw new InvalidOperationException($"No durable job at {key}.");
            if (job.JobType != typeof(ReservationDeadlineJob) || (await scheduler.GetTriggersOfJob(key)).Count != 0)
                throw new InvalidOperationException($"Job {key} is not an exhausted reservation deadline.");
            await scheduler.TriggerJob(key);
            Console.WriteLine($"Queued exact-key redrive for {key}.");
        }
        finally
        {
            await scheduler.Shutdown();
        }
    }

    public static WebApplication Build(string[] args, Action<WebApplicationBuilder>? configure = null)
        => OperationHost.Build(args, configure);
}
