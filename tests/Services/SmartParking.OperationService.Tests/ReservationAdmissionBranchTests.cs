using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Tests;

public sealed class ReservationAdmissionBranchTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static ReservationRequest NewRequest() => ReservationRequest.AdmitQuote(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "{}", "fingerprint", Now);

    [Theory]
    [InlineData("account")]
    [InlineData("plate")]
    [InlineData("slot")]
    public void TerminalRejectionFencesEveryLateSuccess(string branch)
    {
        var request = NewRequest();
        switch (branch)
        {
            case "account": request.AcceptDecision(request.AttemptId, 1, Guid.NewGuid(), Now, false, Now); break;
            case "plate": request.RecordPlate(false, "51A12345", null, Now); break;
            case "slot": request.RecordSlot(false, null, null); break;
        }
        var before = ReservationProgress.From(request, null);
        request.RecordPlate(true, "51A12345", 1, Now);
        request.RecordSlot(true, Guid.NewGuid(), Guid.NewGuid());
        Assert.False(request.AcceptDecision(request.AttemptId, 1, Guid.NewGuid(), Now, true, Now));
        Assert.Equal(before, ReservationProgress.From(request, null));
        Assert.Equal("REJECTED", request.State);
    }

    [Fact]
    public void HeldTokenRequiresTheSameSnapshotOnReplay()
    {
        var token = ReservationFacilityDependency.Prepare(Guid.NewGuid(), Guid.NewGuid(), "CONFIRMATION",
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "intent", Now.AddMinutes(1), Now);
        var original = """{"complete":true}""";
        var changed = """{"complete":false}""";
        token.RecordHeld(original, FacilityDependencyProtocol.Fingerprint(original), Now);
        token.RecordHeld(original, FacilityDependencyProtocol.Fingerprint(original), Now);
        Assert.Throws<InvalidOperationException>(() =>
            token.RecordHeld(changed, FacilityDependencyProtocol.Fingerprint(changed), Now));
        Assert.Equal(original, token.SnapshotJson);
    }

    [Fact]
    public void ReplayedAccountResultJoinsOnlyAfterBothOwnerChecks()
    {
        var request = NewRequest();
        var originalAttempt = request.AttemptId;
        request.RecordSlot(true, Guid.NewGuid(), Guid.NewGuid());
        request.Advance(Now.AddSeconds(5));
        Assert.True(request.Advance(Now.AddSeconds(15)));
        Assert.False(request.AcceptDecision(originalAttempt, 1, Guid.NewGuid(), Now, true, Now.AddSeconds(16)));
        Assert.True(request.AcceptDecision(request.AttemptId, 2, Guid.NewGuid(), Now, true, Now.AddSeconds(16)));
        Assert.Equal("CHECKING", request.State);
        request.RecordPlate(true, "51A12345", 3, Now.AddSeconds(17));
        Assert.Equal("EVALUATING", request.State);
        Assert.Null(request.ReservationId);
        Assert.Throws<InvalidOperationException>(() => request.AttachQuoteHold(Guid.NewGuid()));
    }
}
