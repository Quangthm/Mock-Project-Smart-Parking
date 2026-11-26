using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.NotificationAuditService.Application.Usecase.AuditProjections.Queries.GetAuditProjectionById;
using SmartParking.NotificationAuditService.Application.Usecase.AuditProjections.Queries.SearchAuditProjections;
using SmartParking.NotificationAuditService.Domain.Entities;
using SmartParking.NotificationAuditService.Tests.TestDoubles;

namespace SmartParking.NotificationAuditService.Tests.Application;

public class SearchAuditProjectionsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ShouldFilterByActorActionLotAndTimeRangeWithPaginationAndRedaction()
    {
        var repo = new FakeCentralAuditProjectionRepository();
        var actorId = Guid.NewGuid();
        var lotId = Guid.NewGuid();
        var t1 = DateTimeOffset.UtcNow.AddMinutes(-10);
        var t2 = DateTimeOffset.UtcNow.AddMinutes(-5);

        var matching = CentralAuditProjection.Create(
            originatingService: "OperationService",
            eventId: Guid.NewGuid(),
            actorId: actorId,
            actorRole: "Admin",
            action: "LOT_POLICY_UPDATED",
            resourceType: "ParkingLot",
            resourceId: lotId,
            details: "{\"lotId\":\"" + lotId + "\",\"apiKey\":\"secret-key\"}",
            occurredAt: t2);

        var outsideTime = CentralAuditProjection.Create(
            originatingService: "UserService",
            eventId: Guid.NewGuid(),
            actorId: Guid.NewGuid(),
            actorRole: "Driver",
            action: "LOGIN_FAILED",
            resourceType: "User",
            resourceId: Guid.NewGuid(),
            details: "{\"reason\":\"bad password\"}",
            occurredAt: t1);

        await repo.AddAsync(matching);
        await repo.AddAsync(outsideTime);

        var handler = new SearchAuditProjectionsQueryHandler(
            repo,
            NullLogger<SearchAuditProjectionsQueryHandler>.Instance);

        var result = await handler.Handle(
            new SearchAuditProjectionsQuery(
                ActorId: actorId,
                Action: "LOT_POLICY",
                LotId: lotId,
                From: t2.AddMinutes(-1),
                To: t2.AddMinutes(1),
                Page: 1,
                PageSize: 10),
            CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(1, result.TotalPages);
        Assert.True(result.ReadModel.IsAsynchronousProjection);

        var item = Assert.Single(result.Items);
        Assert.Equal(matching.Id, item.Id);
        Assert.Contains("[REDACTED]", item.Details);
        Assert.DoesNotContain("secret-key", item.Details);
        Assert.True(item.IngestionLagSeconds >= 0);
    }

    [Fact]
    public async Task GetById_WhenMissing_ShouldThrowKeyNotFoundException()
    {
        var repo = new FakeCentralAuditProjectionRepository();
        var handler = new GetAuditProjectionByIdQueryHandler(
            repo,
            NullLogger<GetAuditProjectionByIdQueryHandler>.Instance);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            handler.Handle(new GetAuditProjectionByIdQuery(Guid.NewGuid()), CancellationToken.None));
    }
}
