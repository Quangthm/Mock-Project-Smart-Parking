using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Tests;

public sealed class ReservationAdmissionBranchTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;
    private static ReservationRequest NewRequest() => ReservationRequest.AdmitQuote(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "{}", "fingerprint", Now, TimeSpan.FromSeconds(15));

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
    public void ReplayedAccountResultJoinsOnlyAfterBothOwnerChecks()
    {
        var request = NewRequest();
        var originalAttempt = request.AttemptId;
        request.RecordSlot(true, Guid.NewGuid(), Guid.NewGuid());
        Assert.False(request.AcceptDecision(originalAttempt, 2, Guid.NewGuid(), Now, true, Now.AddSeconds(6)));
        Assert.True(request.AcceptDecision(originalAttempt, 1, Guid.NewGuid(), Now, true, Now.AddSeconds(6)));
        Assert.Equal("CHECKING", request.State);
        request.RecordPlate(true, "51A12345", 3, Now.AddSeconds(7));
        Assert.Equal("EVALUATING", request.State);
        Assert.Null(request.ReservationId);
        Assert.Throws<InvalidOperationException>(() => request.CompleteAdmission(Guid.NewGuid(), false));
    }
}
