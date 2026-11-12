using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.NotificationAuditService.Application.Usecase.AuditProjections;
using SmartParking.NotificationAuditService.Domain.Entities;
using SmartParking.NotificationAuditService.Tests.TestDoubles;

namespace SmartParking.NotificationAuditService.Tests.Application;

public class RecordAuditProjectionCommandHandlerTests
{
    private readonly FakeCentralAuditProjectionRepository _fakeRepo = new();
    private readonly FakeUnitOfWork _fakeUow = new();
    private readonly NullLogger<RecordAuditProjectionCommandHandler> _logger = NullLogger<RecordAuditProjectionCommandHandler>.Instance;
    private readonly RecordAuditProjectionCommandValidator _validator = new();

    private RecordAuditProjectionCommandHandler CreateHandler()
    {
        return new RecordAuditProjectionCommandHandler(_fakeRepo, _fakeUow, _logger);
    }

    [Fact]
    public async Task Handle_WithValidNewEvent_ShouldPersistProjectionAndCommit()
    {
        var handler = CreateHandler();
        var command = new RecordAuditProjectionCommand
        {
            EventId = Guid.NewGuid(),
            OriginatingService = "ReservationService",
            ActorId = Guid.NewGuid(),
            ActorRole = "Driver",
            Action = "ReservationCreated",
            ResourceType = "Reservation",
            ResourceId = Guid.NewGuid(),
            Details = "{\"slot\": \"A-08\"}",
            OccurredAt = DateTimeOffset.UtcNow
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsDuplicate);
        Assert.NotNull(result.ProjectionId);
        Assert.Single(_fakeRepo.Projections);
        Assert.Equal(1, _fakeUow.SaveCount);

        var saved = _fakeRepo.Projections.First();
        Assert.Equal(command.EventId, saved.EventId);
        Assert.Equal("ReservationService", saved.OriginatingService);
        Assert.Equal("ReservationCreated", saved.Action);
    }

    [Fact]
    public async Task Handle_WithDuplicateEventId_ShouldDeduplicateAndNotInsertAgain()
    {
        var handler = CreateHandler();
        var eventId = Guid.NewGuid();

        // Seed existing projection
        var existing = CentralAuditProjection.Create(
            "ReservationService",
            eventId,
            Guid.NewGuid(),
            "Driver",
            "ReservationCreated",
            "Reservation",
            Guid.NewGuid(),
            "{}",
            DateTimeOffset.UtcNow);
        await _fakeRepo.AddAsync(existing);

        var command = new RecordAuditProjectionCommand
        {
            EventId = eventId,
            OriginatingService = "ReservationService",
            ActorId = Guid.NewGuid(),
            ActorRole = "Driver",
            Action = "ReservationCreated",
            ResourceType = "Reservation",
            ResourceId = Guid.NewGuid(),
            Details = "{}",
            OccurredAt = DateTimeOffset.UtcNow
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.IsDuplicate);
        Assert.Null(result.ProjectionId);
        Assert.Single(_fakeRepo.Projections); // Remains 1, no second insertion
        Assert.Equal(0, _fakeUow.SaveCount); // UoW not called
    }

    [Fact]
    public void Validator_WithValidCommand_ShouldPassValidation()
    {
        var command = new RecordAuditProjectionCommand
        {
            EventId = Guid.NewGuid(),
            OriginatingService = "PaymentService",
            ActorId = Guid.NewGuid(),
            ActorRole = "System",
            Action = "PaymentVerified",
            ResourceType = "PaymentOrder",
            ResourceId = Guid.NewGuid(),
            Details = "{\"amount\": 50000}",
            OccurredAt = DateTimeOffset.UtcNow
        };

        var validationResult = _validator.Validate(command);

        Assert.True(validationResult.IsValid);
    }

    [Theory]
    [InlineData("", "Driver", "Action", "Resource")]
    [InlineData("Service", "", "Action", "Resource")]
    [InlineData("Service", "Driver", "", "Resource")]
    [InlineData("Service", "Driver", "Action", "")]
    public void Validator_WithMissingRequiredStringFields_ShouldFailValidation(
        string service,
        string role,
        string action,
        string resource)
    {
        var command = new RecordAuditProjectionCommand
        {
            EventId = Guid.NewGuid(),
            OriginatingService = service,
            ActorId = Guid.NewGuid(),
            ActorRole = role,
            Action = action,
            ResourceType = resource,
            ResourceId = Guid.NewGuid(),
            OccurredAt = DateTimeOffset.UtcNow
        };

        var validationResult = _validator.Validate(command);

        Assert.False(validationResult.IsValid);
    }

    [Fact]
    public void Validator_WithEmptyGuids_ShouldFailValidation()
    {
        var command = new RecordAuditProjectionCommand
        {
            EventId = Guid.Empty,
            OriginatingService = "Service",
            ActorId = Guid.Empty,
            ActorRole = "Role",
            Action = "Action",
            ResourceType = "Resource",
            ResourceId = Guid.Empty,
            OccurredAt = DateTimeOffset.UtcNow
        };

        var validationResult = _validator.Validate(command);

        Assert.False(validationResult.IsValid);
        Assert.Equal(3, validationResult.Errors.Count); // EventId, ActorId, ResourceId
    }

    [Fact]
    public void Validator_WithEmptyDetails_ShouldFailValidation()
    {
        var command = new RecordAuditProjectionCommand
        {
            EventId = Guid.NewGuid(),
            OriginatingService = "Service",
            ActorId = Guid.NewGuid(),
            ActorRole = "Role",
            Action = "Action",
            ResourceType = "Resource",
            ResourceId = Guid.NewGuid(),
            Details = "",
            OccurredAt = DateTimeOffset.UtcNow
        };

        var validationResult = _validator.Validate(command);

        Assert.False(validationResult.IsValid);
        Assert.Contains(validationResult.Errors, e => e.PropertyName == "Details");
    }
}
