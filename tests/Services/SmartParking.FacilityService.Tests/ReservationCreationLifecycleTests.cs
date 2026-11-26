using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.OperationService.Application;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;
using SmartParking.OperationService.Infrastructure.Reservations;

namespace SmartParking.FacilityService.Tests;

public sealed class ReservationCreationLifecycleTests
{
    [Fact]
    public void HoldCommitTerminatesRequestAndLeavesReservationPendingPayment()
    {
        var now = DateTimeOffset.UtcNow;
        var request = ReservationRequest.AdmitQuote(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "{}", "fingerprint", now, TimeSpan.FromSeconds(15));
        Assert.True(request.AcceptDecision(request.AttemptId, 1, Guid.NewGuid(),
            now.AddSeconds(1), true, now.AddSeconds(1)));
        request.RecordPlate(true, "51A12345", 1, now.AddSeconds(2));
        request.RecordSlot(true, Guid.NewGuid(), Guid.NewGuid());
        var booking = CreateBooking(request.Id, now.AddSeconds(2), 20000, 20000);

        request.CompleteAdmission(booking.Id, false);

        Assert.Equal("HOLD_CREATED", request.State);
        Assert.Equal(booking.Id, request.ReservationId);
        Assert.Equal(request.Id, booking.RequestId);
        Assert.Equal("PENDING_PAYMENT", booking.Status);
        Assert.Equal(booking.TargetSlotId, booking.ActiveClaimSlotId);
        Assert.Equal(now.AddSeconds(2).AddMinutes(5), booking.HoldExpiresAt);
    }

    [Fact]
    public void ConfirmedZeroBookingCompletesBeforeReceipt()
    {
        var now = DateTimeOffset.UtcNow;
        var booking = CreateBooking(Guid.NewGuid(), now, 20000, 0);
        booking.ConfirmZeroAdmission(now);
        var effect = booking.ConfirmationEffectId;
        Assert.Null(booking.PaymentOrderId);
        Assert.Null(booking.ZeroReceiptId);
        booking.EnterParking();
        booking.CompleteParking();
        var order = Guid.NewGuid();
        var receipt = Guid.NewGuid();
        Assert.True(booking.LinkZeroReceipt(order, receipt));
        Assert.False(booking.LinkZeroReceipt(order, receipt));
        Assert.Equal("COMPLETED", booking.Status);
        Assert.Null(booking.ActiveClaimSlotId);
        Assert.Equal(effect, booking.ConfirmationEffectId);
    }

    private static ReservationRow CreateBooking(Guid requestId, DateTimeOffset now, decimal booked, decimal deposit) =>
        ReservationRow.PrepareQuote(requestId, Guid.NewGuid(), "51A12345", Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), "PAYOS", now.AddHours(1), now.AddHours(2), now.AddHours(2), now, deposit > 0 ? now.AddMinutes(5) : null,
            booked, booked - deposit, deposit, "VND", "{}", Guid.NewGuid(), now.AddMinutes(1),
            "tariff", "coverage", booked > 0 && deposit == 0);
}
