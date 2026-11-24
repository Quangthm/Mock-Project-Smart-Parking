using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Infrastructure.Reservations;

namespace SmartParking.OperationService.Tests;

// Fail-closed coverage for the durable messaging gate: booking facts must surface a
// 503/RESERVATION_MESSAGING_UNAVAILABLE gate, while the non-booking structure fact is a
// best-effort no-op so a disabled broker cannot fail the owning structure-edit workflow.
public sealed class DisabledReservationFactsPublisherTests
{
    [Fact]
    public async Task FacilityAppliedAsync_CompletesWithoutThrowing()
    {
        var publisher = new DisabledReservationFactsPublisher(
            NullLogger<DisabledReservationFactsPublisher>.Instance);
        var fact = new FacilityStructureAppliedV1(Guid.NewGuid(), DateTimeOffset.UtcNow,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), true, [], []);

        await publisher.FacilityAppliedAsync(fact, CancellationToken.None);
    }

    [Fact]
    public async Task FacilityAppliedAsync_LogsWarningAndReturns()
    {
        var logger = new CapturingLogger<DisabledReservationFactsPublisher>();
        var publisher = new DisabledReservationFactsPublisher(logger);
        var fact = new FacilityStructureAppliedV1(Guid.NewGuid(), DateTimeOffset.UtcNow,
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), false, [], []);

        var task = publisher.FacilityAppliedAsync(fact, CancellationToken.None);
        await task;

        Assert.True(task.IsCompletedSuccessfully);
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Warning && entry.Message.Contains("FacilityStructureAppliedV1"));
    }

    [Fact]
    public async Task ConfirmedAsync_Throws503ReservationMessagingUnavailable()
    {
        var publisher = new DisabledReservationFactsPublisher(
            NullLogger<DisabledReservationFactsPublisher>.Instance);
        var fact = new ReservationConfirmedV1(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        var error = await Assert.ThrowsAsync<ReservationGateException>(
            () => publisher.ConfirmedAsync(fact, CancellationToken.None));

        Assert.Equal(503, error.StatusCode);
        Assert.Equal("RESERVATION_MESSAGING_UNAVAILABLE", error.Code);
    }

    [Fact]
    public async Task HoldChangedAsync_Throws503ReservationMessagingUnavailable()
    {
        var publisher = new DisabledReservationFactsPublisher(
            NullLogger<DisabledReservationFactsPublisher>.Instance);
        var now = DateTimeOffset.UtcNow;
        var fact = new ReservationHoldChangedV1(Guid.NewGuid(), now, Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            now, now.AddHours(1), now.AddMinutes(15), "PENDING_PAYMENT", 1);

        var error = await Assert.ThrowsAsync<ReservationGateException>(
            () => publisher.HoldChangedAsync(fact, CancellationToken.None));

        Assert.Equal(503, error.StatusCode);
        Assert.Equal("RESERVATION_MESSAGING_UNAVAILABLE", error.Code);
    }

    [Fact]
    public async Task AvailabilityChangedAsync_Throws503ReservationMessagingUnavailable()
    {
        var publisher = new DisabledReservationFactsPublisher(
            NullLogger<DisabledReservationFactsPublisher>.Instance);
        var now = DateTimeOffset.UtcNow;
        var fact = new ReservationAvailabilityChangedV1(Guid.NewGuid(), now, Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), now, now.AddHours(1), "HOLD_RELEASED");

        var error = await Assert.ThrowsAsync<ReservationGateException>(
            () => publisher.AvailabilityChangedAsync(fact, CancellationToken.None));

        Assert.Equal(503, error.StatusCode);
        Assert.Equal("RESERVATION_MESSAGING_UNAVAILABLE", error.Code);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}
