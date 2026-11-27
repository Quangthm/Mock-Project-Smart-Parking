using System.Globalization;
using MediatR;
using Microsoft.Extensions.Logging;
using Quartz;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Infrastructure.Reservations;

[DisallowConcurrentExecution]
[RetryPolicy("00:00:01", "00:00:05", "00:00:15")]
public sealed class ReservationDeadlineJob(IMediator mediator, ILogger<ReservationDeadlineJob> logger) : IJob
{
    public const string Group = "reservation-deadlines";
    internal static JobKey Key(RegisterReservationDeadline d) =>
        new(FormattableString.Invariant($"{d.Kind}-{d.SubjectId:N}-{d.ExpectedAt.UtcTicks}"), Group);

    internal static IJobDetail Build(RegisterReservationDeadline d) =>
        JobBuilder.Create<ReservationDeadlineJob>().WithIdentity(Key(d)).StoreDurably().RequestRecovery()
            .UsingJobData("Kind", d.Kind.ToString()).UsingJobData("SubjectId", d.SubjectId.ToString("D"))
            .UsingJobData("ExpectedAt", d.ExpectedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)).Build();

    internal static RegisterReservationDeadline Read(IJobDetail job)
    {
        var data = job.JobDataMap;
        var d = new RegisterReservationDeadline(Enum.Parse<ReservationDeadlineKind>(data.GetString("Kind")!),
            Guid.Parse(data.GetString("SubjectId")!),
            DateTimeOffset.ParseExact(data.GetString("ExpectedAt")!, "O", CultureInfo.InvariantCulture));
        if (job.JobType != typeof(ReservationDeadlineJob) || !job.Key.Equals(Key(d)) || !Enum.IsDefined(d.Kind))
            throw new InvalidOperationException($"Conflicting deadline identity at {job.Key}.");
        return d;
    }

    internal static ITrigger Trigger(JobKey key, string name, DateTimeOffset at)
    {
        // Quartz acquisition uses milliseconds. Preserve the finer business instant in JobDataMap.
        var milliseconds = at.ToUnixTimeMilliseconds();
        var start = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        if (start < at) start = start.AddMilliseconds(1);
        return TriggerBuilder.Create().WithIdentity(name, Group).ForJob(key).StartAt(start)
            .WithSimpleSchedule(s => s.WithMisfireInstruction(SimpleTriggerMisfireInstruction.FireNow)).Build();
    }

    public async ValueTask Execute(IJobExecutionContext context, CancellationToken ct)
    {
        var job = context.JobDetail;
        var d = Read(job);
        try
        {
            var result = d.Kind == ReservationDeadlineKind.Admission
                ? await mediator.Send(new CheckAdmissionDeadline(d.SubjectId, d.ExpectedAt), ct)
                : await mediator.Send(new ExpireReservationHoldCommand(d.SubjectId, d.ExpectedAt), ct);
            if (result.Remaining is TimeSpan remaining)
            {
                // Account for a scheduler clock ahead of PostgreSQL without changing the business deadline.
                var corrected = context.FireTimeUtc.Add(remaining);
                var next = Trigger(job.Key, $"early-{context.FireInstanceId}", corrected > d.ExpectedAt ? corrected : d.ExpectedAt);
                await context.Scheduler.ScheduleJob(next, cancellationToken: ct);
                return;
            }
            await context.Scheduler.DeleteJob(job.Key, ct);
        }
        catch (Exception error)
        {
            logger.LogError(error, "Deadline {JobKey} {Kind} for {SubjectId} at {ExpectedAt} failed on retry {RetryAttempt}",
                job.Key, d.Kind, d.SubjectId, d.ExpectedAt, context.RetryAttempt);
            throw;
        }
    }
}
