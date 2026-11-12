using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.DTOs;
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

    [Fact]
    public async Task HoldCreatedEventTriggersPositivePriceOrderProvisioning()
    {
        var reservationId = Guid.NewGuid();
        var booking = CreateBooking(reservationId, DateTimeOffset.UtcNow, 20000m, 5000m);
        var repo = new FakeBookingRepository(booking);
        var positive = new RecordingHandler<ProvisionReservationOrderCommand>();
        var zero = new RecordingHandler<AcceptZeroPriceTermsCommand>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IRequestHandler<ProvisionReservationOrderCommand>>(positive);
        services.AddSingleton<IRequestHandler<AcceptZeroPriceTermsCommand>>(zero);
        var mediator = new Mediator(services.BuildServiceProvider());

        await ReservationHoldChangedConsumer.DispatchAsync(repo, mediator,
            new ReservationHoldChangedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), reservationId,
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(1),
                DateTimeOffset.UtcNow.AddHours(2), DateTimeOffset.UtcNow.AddMinutes(5), "PENDING_PAYMENT", 1),
            CancellationToken.None);

        Assert.Single(positive.Calls);
        Assert.Empty(zero.Calls);
    }

    [Fact]
    public async Task HoldCreatedEventTriggersZeroPriceAcceptance()
    {
        var reservationId = Guid.NewGuid();
        var booking = CreateBooking(reservationId, DateTimeOffset.UtcNow, 0m, 0m);
        var repo = new FakeBookingRepository(booking);
        var positive = new RecordingHandler<ProvisionReservationOrderCommand>();
        var zero = new RecordingHandler<AcceptZeroPriceTermsCommand>();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IRequestHandler<ProvisionReservationOrderCommand>>(positive);
        services.AddSingleton<IRequestHandler<AcceptZeroPriceTermsCommand>>(zero);
        var mediator = new Mediator(services.BuildServiceProvider());

        await ReservationHoldChangedConsumer.DispatchAsync(repo, mediator,
            new ReservationHoldChangedV1(Guid.NewGuid(), DateTimeOffset.UtcNow, Guid.NewGuid(), reservationId,
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(1),
                DateTimeOffset.UtcNow.AddHours(2), DateTimeOffset.UtcNow.AddMinutes(5), "PENDING_PAYMENT", 1),
            CancellationToken.None);

        Assert.Empty(positive.Calls);
        Assert.Single(zero.Calls);
    }

    private static ReservationRow CreateBooking(Guid requestId, DateTimeOffset now, decimal booked, decimal deposit) =>
        ReservationRow.Prepare(Guid.NewGuid(), requestId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "terms-v1", "PAYOS", now.AddHours(1),
            now.AddHours(2), now.AddHours(2), now, now.AddMinutes(5), booked, deposit, "VND", "{}", "{}");

    private sealed class FakeBookingRepository(ReservationRow booking) : IReservationBookingRepository
    {
        public Task AddAsync(ReservationRow entity, CancellationToken cancellationToken) => Task.CompletedTask;
        public void Remove(ReservationRow entity) { }
        public Task<ReservationRow?> FindForUpdateAsync(Guid tenantId, Guid siteId, Guid reservationId, CancellationToken cancellationToken) =>
            Task.FromResult<ReservationRow?>(booking.Id == reservationId ? booking : null);
        public Task<IReadOnlyList<ReservationGateEntry>> ListAsync(Guid tenantId, Guid siteId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ReservationGateEntry>>([]);
        public Task<ReservationGateEntry?> FindAsync(Guid tenantId, Guid siteId, string query, CancellationToken ct) =>
            Task.FromResult<ReservationGateEntry?>(null);
        public Task<ReservationGateEntry?> FindByIdAsync(Guid tenantId, Guid siteId, Guid reservationId, CancellationToken ct) =>
            Task.FromResult<ReservationGateEntry?>(null);
        public Task<ReservationRow?> FindOwnBookingAsync(Guid accountId, Guid id, CancellationToken ct) =>
            Task.FromResult<ReservationRow?>(null);
        public Task<IReadOnlyList<ReservationRow>> ListOwnBookingsAsync(Guid accountId, int take, DateTimeOffset? before, Guid? beforeId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ReservationRow>>([]);
        public Task<ReservationRow?> FindOrderProvisioningAsync(Guid reservationId, CancellationToken ct) =>
            Task.FromResult<ReservationRow?>(booking.Id == reservationId ? booking : null);
        public Task<bool> OrderAssignedToAnotherReservationAsync(Guid orderId, Guid reservationId, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyList<Guid>> DueOrderProvisioningAsync(DateTimeOffset now, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<IReadOnlyList<Guid>> DueHoldExpiryAsync(DateTimeOffset now, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<IReadOnlyList<Guid>> DueZeroPriceAcceptanceAsync(DateTimeOffset now, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<IReadOnlyList<Guid>> DueNoPaymentConfirmationAsync(DateTimeOffset now, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<IReadOnlyList<Guid>> DuePaidConfirmationAsync(DateTimeOffset now, int take, CancellationToken ct) => Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<DateTimeOffset> DatabaseNowAsync(CancellationToken ct) => Task.FromResult(DateTimeOffset.UtcNow);
        public Task RequireDurableConfirmationAsync(CancellationToken ct) => Task.CompletedTask;
        public Task RequirePlateAvailableAsync(ReservationRow proposed, DateTimeOffset now, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class RecordingHandler<TCommand> : IRequestHandler<TCommand>
        where TCommand : IRequest
    {
        public List<object> Calls { get; } = [];

        public Task Handle(TCommand request, CancellationToken cancellationToken)
        {
            Calls.Add(request!);
            return Task.CompletedTask;
        }
    }
}
