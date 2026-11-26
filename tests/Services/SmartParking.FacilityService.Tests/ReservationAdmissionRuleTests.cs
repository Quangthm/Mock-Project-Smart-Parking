using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.FacilityService.Tests;

public sealed class ReservationAdmissionRuleTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 0, 0, 0, TimeSpan.Zero);
    private static ReservationRequest Request() => ReservationRequest.AdmitQuote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "{}", "fingerprint", Now, TimeSpan.FromSeconds(15));

    [Fact]
    public void SingleDecisionIsBoundToIdentityAndAbsoluteDeadline()
    {
        var request = Request();
        Assert.False(request.AcceptDecision(Guid.NewGuid(), 1, Guid.NewGuid(), Now, true, Now));
        Assert.False(request.AcceptDecision(request.AttemptId, 2, Guid.NewGuid(), Now, true, Now));
        Assert.False(request.AcceptDecision(request.AttemptId, 1, Guid.NewGuid(), Now, true, request.OverallDeadline));
        request.ExpireAdmission();
        Assert.Equal(("FAILED_DEPENDENCY", "ADMISSION_TIMED_OUT"), (request.State, request.Code));
        Assert.False(request.AcceptDecision(request.AttemptId, 1, Guid.NewGuid(), Now, true, Now));
    }

    [Fact]
    public void JoinedDeadlineAndDefinitiveDenialAreDifferentTerminalOutcomes()
    {
        var joined = Request();
        joined.AcceptDecision(joined.AttemptId, 1, Guid.NewGuid(), Now, true, Now);
        joined.RecordPlate(true, "51A12345", 1, Now);
        joined.RecordSlot(true, Guid.NewGuid(), Guid.NewGuid());
        joined.ExpireAdmission();
        Assert.Equal(("REJECTED", "BOOKING_PREPARATION_EXPIRED"), (joined.State, joined.Code));
        var denied = Request();
        denied.AcceptDecision(denied.AttemptId, 1, Guid.NewGuid(), Now, false, Now);
        denied.ExpireAdmission();
        Assert.Equal(("REJECTED", "CURRENT_ACCESS_DENIED"), (denied.State, denied.Code));
    }

    [Fact]
    public void AllowedRequiresBothOwnerChecksBeforeAttachingTheHold()
    {
        var request = Request();
        Assert.True(request.AcceptDecision(request.AttemptId, 1, Guid.NewGuid(), Now.AddSeconds(1), true, Now.AddSeconds(2)));
        Assert.Throws<InvalidOperationException>(() => request.CompleteAdmission(request.Id, false));
        request.RecordPlate(true, "51A12345", 7, Now.AddSeconds(3));
        request.RecordPlate(false, "51A12345", null, Now.AddSeconds(4));
        Assert.Equal("51A12345", request.VehiclePlate);
        Assert.Equal(7L, request.VehicleBindingRevision);
        Assert.Throws<InvalidOperationException>(() => request.CompleteAdmission(request.Id, false));
        request.RecordSlot(true, Guid.NewGuid(), Guid.NewGuid());
        request.CompleteAdmission(request.Id, false);
        Assert.Equal(request.Id, request.ReservationId);
        Assert.False(request.AcceptDecision(request.AttemptId, 1, Guid.NewGuid(), Now.AddSeconds(1), true, Now.AddSeconds(3)));
    }

    [Fact]
    public void ReceiptMappingAndInputValidationDoNotExposeTrustedActorOrCreateBooking()
    {
        var configuration = new MapperConfiguration(config => config.AddProfile<AdmissionMapperProfile>(), NullLoggerFactory.Instance);
        configuration.AssertConfigurationIsValid();
        var receipt = configuration.CreateMapper().Map<ReservationReceipt>(Request());
        Assert.Null(receipt.ReservationId);
        Assert.Equal("CHECKING", receipt.State);
        var invalid = new AdmitQuoteReservation(new("", Guid.Empty, Now, Now, "CASH", Guid.Empty), Guid.Empty);
        Assert.False(new AdmitQuoteReservationValidator().Validate(invalid).IsValid);
    }
}
