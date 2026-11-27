using MassTransit;
using Quartz;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class RegisterReservationDeadlineConsumer(ISchedulerFactory factory, IReservationRequestRepository requests,
    IReservationBookingRepository bookings) : IConsumer<RegisterReservationDeadline>
{
    public async Task Consume(ConsumeContext<RegisterReservationDeadline> context)
    {
        var d = context.Message;
        var ct = context.CancellationToken;
        var current = d.Kind switch
        {
            ReservationDeadlineKind.Admission => await requests.FindForWorkerAsync(d.SubjectId, ct) is
                { State: "CHECKING" or "EVALUATING" } request && request.OverallDeadline == d.ExpectedAt,
            ReservationDeadlineKind.UnpaidHold => await bookings.FindBookingAsync(d.SubjectId, ct) is
                { Status: "PENDING_PAYMENT", DepositAmount: > 0, QuoteId: not null } booking && booking.HoldExpiresAt == d.ExpectedAt,
            _ => throw new ArgumentOutOfRangeException(nameof(d.Kind))
        };
        if (!current) return;
        var scheduler = await factory.GetScheduler(ct);
        var job = ReservationDeadlineJob.Build(d);
        try
        {
            await scheduler.ScheduleJob(job, ReservationDeadlineJob.Trigger(job.Key, job.Key.Name, d.ExpectedAt), cancellationToken: ct);
        }
        catch (ObjectAlreadyExistsException)
        {
            var existing = await scheduler.GetJobDetail(job.Key, ct);
            if (existing is null) throw; // Cleanup won the race. Redelivery checks authoritative state again.
            if (ReservationDeadlineJob.Read(existing) != d)
                throw new InvalidOperationException($"Contradictory reservation deadline at {job.Key}.");
        }
    }
}
