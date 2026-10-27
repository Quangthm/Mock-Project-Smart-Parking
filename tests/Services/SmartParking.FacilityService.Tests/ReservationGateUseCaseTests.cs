using SmartParking.ReservationService.Application.Common.Interfaces.Persistence;
using SmartParking.ReservationService.Application.Common.Interfaces.Services;
using SmartParking.ReservationService.Application.Common.Models.Exceptions;
using SmartParking.ReservationService.Application.DTOs;
using SmartParking.ReservationService.Application.Usecase.ReservationGate;

namespace SmartParking.FacilityService.Tests;

public sealed class ReservationGateUseCaseTests
{
    private readonly Guid tenantId = Guid.NewGuid();
    private readonly Guid userId = Guid.NewGuid();
    private readonly Guid siteId = Guid.NewGuid();

    private (FakeOperatorIdentity, FakeGateStore) CreateMocks()
    {
        var identity = new FakeOperatorIdentity(new OperatorSiteScope(userId, tenantId, siteId));
        var store = new FakeGateStore();
        return (identity, store);
    }

    [Fact]
    public async Task ListReservationsQueryHandler_ReturnsStoreList()
    {
        var (identity, store) = CreateMocks();
        var handler = new ListReservationsQueryHandler(identity, store);

        var result = await handler.Handle(new ListReservationsQuery(siteId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(identity.WasCalled);
    }

    [Fact]
    public async Task ListParkingSessionsQueryHandler_ReturnsStoreList()
    {
        var (identity, store) = CreateMocks();
        var handler = new ListParkingSessionsQueryHandler(identity, store);

        var result = await handler.Handle(new ListParkingSessionsQuery(siteId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(identity.WasCalled);
    }

    [Fact]
    public async Task FindReservationQueryHandler_ThrowsOnInvalidQuery()
    {
        var (identity, store) = CreateMocks();
        var handler = new FindReservationQueryHandler(identity, store);

        var ex = await Assert.ThrowsAsync<ReservationGateException>(() =>
            handler.Handle(new FindReservationQuery(siteId, "  "), CancellationToken.None));
        Assert.Equal("INVALID_QUERY", ex.Code);
    }

    [Fact]
    public async Task FindReservationQueryHandler_ThrowsWhenNotFound()
    {
        var (identity, store) = CreateMocks();
        store.FoundReservation = null;
        var handler = new FindReservationQueryHandler(identity, store);

        var ex = await Assert.ThrowsAsync<ReservationGateException>(() =>
            handler.Handle(new FindReservationQuery(siteId, "29A12345"), CancellationToken.None));
        Assert.Equal("RESERVATION_NOT_FOUND", ex.Code);
    }

    [Fact]
    public async Task FindReservationQueryHandler_ReturnsReservationWhenFound()
    {
        var (identity, store) = CreateMocks();
        var entry = CreateSampleReservationEntry();
        store.FoundReservation = entry;
        var handler = new FindReservationQueryHandler(identity, store);

        var result = await handler.Handle(new FindReservationQuery(siteId, "29A12345"), CancellationToken.None);
        Assert.Equal(entry.Id, result.Id);
    }

    [Fact]
    public async Task FindWalkInSessionQueryHandler_ThrowsOnInvalidPlateOrNotFound()
    {
        var (identity, store) = CreateMocks();
        var handler = new FindWalkInSessionQueryHandler(identity, store);

        await Assert.ThrowsAsync<ReservationGateException>(() =>
            handler.Handle(new FindWalkInSessionQuery(siteId, ""), CancellationToken.None));

        store.FoundWalkIn = null;
        var notFoundEx = await Assert.ThrowsAsync<ReservationGateException>(() =>
            handler.Handle(new FindWalkInSessionQuery(siteId, "29A-99999"), CancellationToken.None));
        Assert.Equal("WALK_IN_SESSION_NOT_FOUND", notFoundEx.Code);
    }

    [Fact]
    public async Task FindWalkInSessionQueryHandler_ReturnsSession()
    {
        var (identity, store) = CreateMocks();
        var session = CreateSampleWalkIn();
        store.FoundWalkIn = session;
        var handler = new FindWalkInSessionQueryHandler(identity, store);

        var result = await handler.Handle(new FindWalkInSessionQuery(siteId, "29A-99999"), CancellationToken.None);
        Assert.Equal(session.Id, result.Id);
    }

    [Fact]
    public async Task CheckInWalkInCommandHandler_ValidatesAndCreates()
    {
        var (identity, store) = CreateMocks();
        var handler = new CheckInWalkInCommandHandler(identity, store);

        // Invalid vehicle type
        await Assert.ThrowsAsync<ReservationGateException>(() =>
            handler.Handle(new CheckInWalkInCommand(siteId, "29A-12345", null, "HELICOPTER"), CancellationToken.None));

        // Invalid image URL
        await Assert.ThrowsAsync<ReservationGateException>(() =>
            handler.Handle(new CheckInWalkInCommand(siteId, "29A-12345", "http://bad-image.com", "CAR"), CancellationToken.None));

        // Valid walk-in
        var validImage = "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";
        var result = await handler.Handle(new CheckInWalkInCommand(siteId, "29A-12345", validImage, "CAR"), CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task CheckOutWalkInCommandHandler_ValidatesAndChecksOut()
    {
        var (identity, store) = CreateMocks();
        var handler = new CheckOutWalkInCommandHandler(identity, store);

        var validImage = "data:image/jpeg;base64,/9j/4AAQSkZJRg==";
        var result = await handler.Handle(new CheckOutWalkInCommand(siteId, Guid.NewGuid(), "29A-12345", validImage), CancellationToken.None);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task CheckInAndOutReservationCommandHandler_ValidatesAndProcesses()
    {
        var (identity, store) = CreateMocks();
        var checkInHandler = new CheckInReservationCommandHandler(identity, store);
        var checkOutHandler = new CheckOutReservationCommandHandler(identity, store);

        var resId = Guid.NewGuid();
        var validImage = "data:image/webp;base64,UklGRhoAAABXRUJQVlA4TA0AAAAvAAAAEAcQERGIiP4HAA==";

        var inResult = await checkInHandler.Handle(new CheckInReservationCommand(siteId, resId, "29A-12345", validImage), CancellationToken.None);
        Assert.NotNull(inResult);

        var outResult = await checkOutHandler.Handle(new CheckOutReservationCommand(siteId, resId, "29A-12345", validImage), CancellationToken.None);
        Assert.NotNull(outResult);
    }

    private static ReservationGateEntry CreateSampleReservationEntry() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Test Site", Guid.NewGuid(), "A01", "29A-12345",
            "CONFIRMED", "PAID", "CARD", 50000m, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(2),
            null, null, null, null, DateTimeOffset.UtcNow);

    private static WalkInParkingSession CreateSampleWalkIn() =>
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "A01", "29A-99999", "CAR",
            DateTimeOffset.UtcNow, null, null, null);

    private sealed class FakeOperatorIdentity(OperatorSiteScope scope) : IOperatorIdentityClient
    {
        public bool WasCalled { get; private set; }
        public Task<OperatorSiteScope> RequireSiteAsync(Guid siteId, CancellationToken ct)
        {
            WasCalled = true;
            return Task.FromResult(scope);
        }
    }

    private sealed class FakeGateStore : IReservationGateStore
    {
        public ReservationGateEntry? FoundReservation { get; set; }
        public WalkInParkingSession? FoundWalkIn { get; set; }

        public Task<IReadOnlyList<ReservationGateEntry>> ListAsync(Guid tenantId, Guid siteId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ReservationGateEntry>>([CreateSampleReservationEntry()]);

        public Task<IReadOnlyList<ParkingSessionHistoryEntry>> ListSessionsAsync(Guid tenantId, Guid siteId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<ParkingSessionHistoryEntry>>([]);

        public Task<ReservationGateEntry?> FindAsync(Guid tenantId, Guid siteId, string query, CancellationToken ct)
            => Task.FromResult(FoundReservation);

        public Task<WalkInParkingSession?> FindActiveWalkInAsync(Guid tenantId, Guid siteId, string plate, CancellationToken ct)
            => Task.FromResult(FoundWalkIn);

        public Task<WalkInParkingSession> CreateWalkInAsync(Guid tenantId, Guid siteId, Guid sessionId, string vehicleType, string plate, string? imageData, CancellationToken ct)
            => Task.FromResult(CreateSampleWalkIn());

        public Task<WalkInParkingSession> CheckOutWalkInAsync(Guid tenantId, Guid siteId, Guid sessionId, string plate, string? imageData, CancellationToken ct)
            => Task.FromResult(CreateSampleWalkIn());

        public Task<ReservationGateEntry> CheckInAsync(Guid tenantId, Guid siteId, Guid actorId, Guid reservationId, string? recognizedPlate, string? imageData, CancellationToken ct)
            => Task.FromResult(CreateSampleReservationEntry());

        public Task<ReservationGateEntry> CheckOutAsync(Guid tenantId, Guid siteId, Guid actorId, Guid reservationId, string? recognizedPlate, string? imageData, CancellationToken ct)
            => Task.FromResult(CreateSampleReservationEntry());
    }
}
