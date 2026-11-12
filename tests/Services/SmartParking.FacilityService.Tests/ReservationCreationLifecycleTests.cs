using SmartParking.OperationService.Domain.Entities;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.FacilityService.Tests;

public sealed class ReservationCreationLifecycleTests
{
    [Fact]
    public void HoldCommitTerminatesRequestAndLeavesReservationPendingPayment()
    {
        var now = DateTimeOffset.UtcNow;
        var request = ReservationRequest.Admit(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "{}", "fingerprint", now);
        Assert.True(request.AcceptDecision(request.AttemptId, request.AttemptNumber, Guid.NewGuid(),
            now.AddSeconds(1), true, now.AddSeconds(1)));
        request.RecordVehicleBinding(true, 1, "ABC123", now.AddSeconds(2));
        var booking = CreateBooking(request.Id, now.AddSeconds(2), 20000, 5000);

        request.AttachBooking(booking.Id);

        Assert.Equal("HOLD_CREATED", request.State);
        Assert.Equal(booking.Id, request.ReservationId);
        Assert.Equal(request.Id, booking.RequestId);
        Assert.Equal("PENDING_PAYMENT", booking.Status);
        Assert.Equal(booking.TargetSlotId, booking.ActiveClaimSlotId);
        Assert.Equal(now.AddSeconds(2).AddMinutes(5), booking.HoldExpiresAt);
    }

    [Fact]
    public void OrderProvisioningReplaysUnknownWithStableReservationOrderIdentity()
    {
        var now = DateTimeOffset.UtcNow;
        var booking = CreateBooking(Guid.NewGuid(), now, 20000, 5000);

        Assert.Equal("PENDING", booking.OrderProvisioningState);
        Assert.True(booking.BeginOrderProvisioning(now, TimeSpan.FromSeconds(30)));
        Assert.Equal(1, booking.OrderProvisioningAttempts);
        booking.DeferOrderProvisioning(now.AddSeconds(31), "UNKNOWN", "PAYMENT_ORDER_OUTCOME_UNKNOWN");

        Assert.False(booking.BeginOrderProvisioning(now.AddSeconds(30), TimeSpan.FromSeconds(30)));
        Assert.True(booking.BeginOrderProvisioning(now.AddSeconds(31), TimeSpan.FromSeconds(30)));
        var orderId = Guid.NewGuid();
        booking.RecordOrderCreated(orderId);

        Assert.Equal("CREATED", booking.OrderProvisioningState);
        Assert.Equal(orderId, booking.PaymentOrderId);
        Assert.Null(booking.OrderProvisioningNextActionAt);
        Assert.Throws<InvalidOperationException>(() => booking.RecordOrderCreated(Guid.NewGuid()));
    }

    [Fact]
    public void ZeroPriceHoldDoesNotCreateAnOrder()
    {
        var booking = CreateBooking(Guid.NewGuid(), DateTimeOffset.UtcNow, 0, 0);

        Assert.Equal("NOT_REQUIRED", booking.OrderProvisioningState);
        Assert.Null(booking.OrderProvisioningNextActionAt);
        Assert.Null(booking.PaymentOrderId);
    }

    [Fact]
    public void ExpiredHoldClosesKnownNoEffectProvisioningWithoutInventingAnOrder()
    {
        var now = DateTimeOffset.UtcNow;
        var booking = CreateBooking(Guid.NewGuid(), now, 20000, 5000);
        Assert.True(booking.BeginOrderProvisioning(now, TimeSpan.FromSeconds(30)));
        booking.DeferOrderProvisioning(now.AddSeconds(31), "PENDING", "PAYMENT_ORDER_PROVISIONING_UNAVAILABLE");

        var expiredAt = booking.HoldExpiresAt!.Value;
        booking.ReleaseExpiredHold(expiredAt);
        booking.CloseOrderProvisioning("PAYMENT_HOLD_EXPIRED");

        Assert.Equal("EXPIRED", booking.Status);
        Assert.Null(booking.ActiveClaimSlotId);
        Assert.Equal("CLOSED_NO_ORDER", booking.OrderProvisioningState);
        Assert.Null(booking.PaymentOrderId);
        Assert.Throws<InvalidOperationException>(() => booking.RecordOrderCreated(Guid.NewGuid()));
    }

    private static ReservationRow CreateBooking(Guid requestId, DateTimeOffset now, decimal booked, decimal deposit) =>
        ReservationRow.Prepare(Guid.NewGuid(), requestId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "terms-v1", "PAYOS", now.AddHours(1),
            now.AddHours(2), now.AddHours(2), now, now.AddMinutes(5), booked, deposit, "VND", "{}", "{}");
}
