using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.FacilityService.Tests;

public sealed class ReservationFacilityDependencyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 10, 8, 0, 0, TimeSpan.Zero);
    private static ReservationFacilityDependency NewDependency() => ReservationFacilityDependency.Prepare(
        Guid.NewGuid(), Guid.NewGuid(), "ADMISSION", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "intent", Now.AddMinutes(1), Now);

    [Fact]
    public void AbortedPreparationCannotAcceptDelayedAcquisitionOrCommit()
    {
        var row = NewDependency();
        row.BeginAbort("DEADLINE", Now.AddMinutes(1));
        Assert.Throws<InvalidOperationException>(() => row.RecordHeld("{}", "hash", Now.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() => row.MarkReady("{}", Now.AddMinutes(1)));
        row.Acknowledge("ABORTED", Now.AddMinutes(2));
        row.Acknowledge("ABORTED", Now.AddMinutes(3));
        Assert.Equal("COMPLETED", row.State);
        Assert.Equal(Now.AddMinutes(2), row.CompletedAt);
    }

    [Fact]
    public void CommittedLocalOutcomeSurvivesDeadlineAndCannotBecomeAbort()
    {
        var row = NewDependency();
        row.RecordHeld("{}", "hash", Now.AddSeconds(1));
        row.MarkReady("{\"reservationId\":\"stable\"}", Now.AddSeconds(2));
        Assert.Throws<InvalidOperationException>(() => row.BeginAbort("DEADLINE", Now.AddMinutes(2)));
        Assert.Throws<InvalidOperationException>(() => row.Acknowledge("ABORTED", Now.AddMinutes(2)));
        row.Acknowledge("COMMITTED", Now.AddMinutes(3));
        Assert.Equal("COMMITTED", row.Resolution);
    }

    [Fact]
    public void ResourceReplayCannotChangeFactsOrExtendAcceptanceDeadline()
    {
        var row = NewDependency();
        row.RecordHeld("{}", "hash", Now.AddSeconds(1));
        row.RecordHeld("{ }", "hash", Now.AddSeconds(2));
        Assert.Equal(Now.AddMinutes(1), row.Deadline);
        Assert.Throws<InvalidOperationException>(() => row.RecordHeld("{}", "other", Now.AddSeconds(3)));
        Assert.Throws<InvalidOperationException>(() => row.MarkReady("{}", row.Deadline));
        row.BeginAbort("DEADLINE", row.Deadline);
        Assert.Throws<InvalidOperationException>(() => row.RecordHeld("{}", "hash", Now.AddMinutes(2)));
    }
}
