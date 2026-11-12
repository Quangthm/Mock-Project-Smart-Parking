using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.FacilityService.Tests;

public sealed class ReservationAdmissionRuleTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 0, 0, 0, TimeSpan.Zero);
    private static ReservationRequest Request() => ReservationRequest.Admit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "{}", "fingerprint", Now);

    [Fact]
    public void TimeoutFencesOldAllowAndNewAttemptsPreserveOriginalDeadline()
    {
        var request = Request();
        var attempt = request.AttemptId;
        Assert.False(request.Advance(Now.AddSeconds(5)));
        Assert.False(request.AcceptDecision(attempt, 1, Guid.NewGuid(), Now.AddSeconds(4), true, Now.AddSeconds(5)));
        Assert.False(request.Advance(Now.AddSeconds(14)));
        Assert.True(request.Advance(Now.AddSeconds(15)));
        Assert.Equal(2, request.AttemptNumber);
        Assert.NotEqual(attempt, request.AttemptId);
        Assert.Equal(Now.AddSeconds(90), request.OverallDeadline);
        Assert.False(request.AcceptDecision(attempt, 1, Guid.NewGuid(), Now.AddSeconds(1), true, Now.AddSeconds(16)));
    }

    [Theory]
    [InlineData(true, "PREPARING")]
    [InlineData(false, "DENIED")]
    public void OriginalWorkflowDecisionCanArriveInTheCurrentRetryEnvelope(bool allowed, string expected)
    {
        var request = Request(); var oldAttempt = request.AttemptId;
        request.Advance(Now.AddSeconds(5)); request.Advance(Now.AddSeconds(15));
        var decisionId = Guid.NewGuid();
        Assert.False(request.AcceptDecision(oldAttempt, 1, decisionId, Now.AddSeconds(1), allowed, Now.AddSeconds(16)));
        Assert.True(request.AcceptDecision(request.AttemptId, 2, decisionId, Now.AddSeconds(1), allowed, Now.AddSeconds(16)));
        Assert.Equal(expected, request.State);
        Assert.Equal(Now.AddSeconds(1), request.DecidedAt);
    }

    [Fact]
    public void DenialAndExhaustedDependencyAreDistinctAndTerminal()
    {
        var denied = Request();
        Assert.True(denied.AcceptDecision(denied.AttemptId, 1, Guid.NewGuid(), Now.AddSeconds(1), false, Now.AddSeconds(2)));
        Assert.Equal("DENIED", denied.State);
        Assert.False(denied.Advance(Now.AddSeconds(100)));
        var absent = Request();
        absent.Advance(Now.AddSeconds(5)); absent.Advance(Now.AddSeconds(15));
        absent.Advance(Now.AddSeconds(20)); Assert.True(absent.Advance(Now.AddSeconds(50)));
        absent.Advance(Now.AddSeconds(55));
        Assert.Equal(3, absent.AttemptNumber);
        Assert.Equal("FAILED_DEPENDENCY", absent.State);
        Assert.Equal("AUTHORIZATION_UNAVAILABLE", absent.Code);
    }

    [Fact]
    public void RestartAfterOverallDeadlineCannotReissue()
    {
        var request = Request();
        Assert.False(request.Advance(Now.AddSeconds(90)));
        Assert.Equal(1, request.AttemptNumber);
        Assert.Equal("FAILED_DEPENDENCY", request.State);
    }

    [Fact]
    public void AllowedRequiresPersistedBindingAndCannotReevaluateIt()
    {
        var request = Request();
        Assert.True(request.AcceptDecision(request.AttemptId, 1, Guid.NewGuid(), Now.AddSeconds(1), true, Now.AddSeconds(2)));
        Assert.Throws<InvalidOperationException>(() => request.AttachBooking(Guid.NewGuid()));
        request.RecordVehicleBinding(true, 7, "51A12345", Now.AddSeconds(3));
        Assert.Throws<InvalidOperationException>(() => request.RecordVehicleBinding(false, null, null, Now.AddSeconds(4)));
        Assert.Equal("51A12345", request.VehiclePlate);
        Assert.Equal(7L, request.VehicleBindingRevision);
        Assert.Null(request.ReservationId);
        Assert.False(request.AcceptDecision(request.AttemptId, 1, Guid.NewGuid(), Now.AddSeconds(1), true, Now.AddSeconds(3)));
    }

    [Fact]
    public void ReceiptMappingAndInputValidationDoNotExposeTrustedActorOrCreateBooking()
    {
        var configuration = new MapperConfiguration(config => config.AddProfile<AdmissionMapperProfile>(), NullLoggerFactory.Instance);
        configuration.AssertConfigurationIsValid();
        var receipt = configuration.CreateMapper().Map<ReservationReceipt>(Request());
        Assert.Null(receipt.ReservationId);
        Assert.Equal("PENDING_AUTHORIZATION", receipt.State);
        var invalid = new AdmitReservationCommand(new(Guid.Empty, Guid.Empty, Guid.Empty,
            "CAPACITY", Now, Now, Guid.Empty, ""), Guid.Empty);
        Assert.False(new AdmitReservationValidator().Validate(invalid).IsValid);
    }
}
