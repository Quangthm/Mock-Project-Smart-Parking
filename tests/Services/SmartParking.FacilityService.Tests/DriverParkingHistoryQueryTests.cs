using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.DTOs;
using SmartParking.OperationService.Application.Usecase.ParkingSessions.Queries.GetDriverParkingHistory;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.FacilityService.Tests;

public sealed class DriverParkingHistoryQueryTests
{
    private static readonly Guid AccountId = Guid.NewGuid();
    private static readonly Guid SessionId = Guid.NewGuid();
    private static readonly Guid SiteId = Guid.NewGuid();
    private static readonly Guid TenantId = Guid.NewGuid();

    private static ParkingSessionHistoryEntry CreateSampleEntry(string status = "ACTIVE", DateTimeOffset? exitTime = null) =>
        new(
            SessionId,
            null,
            TenantId,
            SiteId,
            null,
            AccountId,
            null,
            null,
            "51A-12345",
            "51A12345",
            "CAR",
            "GATE-001",
            DateTimeOffset.UtcNow.AddHours(-1),
            null,
            exitTime,
            null,
            status,
            1,
            DateTimeOffset.UtcNow.AddHours(-1),
            null);

    [Fact]
    public async Task Handle_ReturnsSessionsFromRepository()
    {
        var sample = CreateSampleEntry();
        var repo = new FakeParkingSessionRepository([sample]);
        var handler = new GetDriverParkingHistoryQueryHandler(repo);

        var result = await handler.Handle(new GetDriverParkingHistoryQuery(AccountId, "ALL", 10), CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(SessionId, result[0].Id);
        Assert.Equal(AccountId, result[0].AccountId);
        Assert.Equal("51A-12345", result[0].OriginalPlate);
    }

    [Fact]
    public void Validator_ValidatesAccountIdNotEmpty()
    {
        var validator = new GetDriverParkingHistoryQueryValidator();
        var result = validator.Validate(new GetDriverParkingHistoryQuery(Guid.Empty, "ALL", 10));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == "AccountId");
    }

    [Fact]
    public void Validator_ValidatesLimitRange()
    {
        var validator = new GetDriverParkingHistoryQueryValidator();
        var resultZero = validator.Validate(new GetDriverParkingHistoryQuery(AccountId, "ALL", 0));
        var resultTooHigh = validator.Validate(new GetDriverParkingHistoryQuery(AccountId, "ALL", 101));
        var resultValid = validator.Validate(new GetDriverParkingHistoryQuery(AccountId, "ALL", 50));

        Assert.False(resultZero.IsValid);
        Assert.False(resultTooHigh.IsValid);
        Assert.True(resultValid.IsValid);
    }

    [Fact]
    public void Validator_ValidatesStatusFilter()
    {
        var validator = new GetDriverParkingHistoryQueryValidator();
        var invalidStatus = validator.Validate(new GetDriverParkingHistoryQuery(AccountId, "INVALID_STATUS", 10));
        var validActive = validator.Validate(new GetDriverParkingHistoryQuery(AccountId, "ACTIVE", 10));
        var validCompleted = validator.Validate(new GetDriverParkingHistoryQuery(AccountId, "completed", 10));
        var validNull = validator.Validate(new GetDriverParkingHistoryQuery(AccountId, null, 10));

        Assert.False(invalidStatus.IsValid);
        Assert.True(validActive.IsValid);
        Assert.True(validCompleted.IsValid);
        Assert.True(validNull.IsValid);
    }

    private sealed class FakeParkingSessionRepository(IReadOnlyList<ParkingSessionHistoryEntry> entries)
        : IParkingSessionRepository
    {
        public Task<IReadOnlyList<ParkingSessionHistoryEntry>> ListSessionsByAccountAsync(
            Guid accountId, string? statusFilter, int limit, CancellationToken ct) =>
            Task.FromResult(entries);

        public Task<ParkingSessionRow?> FindForUpdateAsync(Guid tenantId, Guid siteId, Guid sessionId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public Task<IReadOnlyList<ParkingSessionHistoryEntry>> ListSessionsAsync(Guid tenantId, Guid siteId, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task<WalkInParkingSession?> FindActiveWalkInAsync(Guid tenantId, Guid siteId, string plate, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task<bool> HasActivePlateAsync(Guid tenantId, Guid siteId, string plate, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task<ParkingSessionRow?> FindByReservationAsync(Guid tenantId, Guid siteId, Guid reservationId, bool active, CancellationToken ct) =>
            throw new NotImplementedException();
        public Task AddAsync(ParkingSessionRow entity, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
        public void Remove(ParkingSessionRow entity) =>
            throw new NotImplementedException();
    }
}
