using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Application.Common.Behaviors;
using SmartParking.FacilityService.Application.Common.Security;
using SmartParking.FacilityService.Application.DTOs;
using SmartParking.FacilityService.Application.Usecases.Structure.Commands.BatchEditStructure;
using SmartParking.FacilityService.Application.Usecases.Structure.Commands.MarkBackup;
using SmartParking.FacilityService.Application.Usecases.Structure.Queries.GetParkingStructure;
using SmartParking.FacilityService.Domain;
using Xunit;

namespace SmartParking.FacilityService.Tests;

public sealed class CqrsAndSecurityVerificationTests
{
    private static readonly Guid TenantId = Guid.NewGuid();
    private static readonly Guid SiteId = Guid.NewGuid();
    private static readonly Guid SlotId = Guid.NewGuid();
    private static readonly OwnerScope Scope = new(Guid.NewGuid(), TenantId);

    [Fact]
    public async Task GetParkingStructureQueryHandler_PerformsPureLockFreeReadWithoutSnapshot()
    {
        // Arrange
        var fakeRepo = new StubReadRepository();
        var fakeScopeResolver = new StubScopeResolver(Scope);
        var fakeWindowStore = new StubMaintenanceWindowStore();
        var fakeAuthorizer = new StubOwnerAuthorizer();

        var handler = new GetParkingStructureQueryHandler(
            fakeScopeResolver,
            fakeAuthorizer,
            fakeRepo,
            fakeWindowStore,
            NullLogger<GetParkingStructureQueryHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetParkingStructureQuery(SiteId, TenantId), CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(SiteId, result.Site.Id);
        Assert.Null(fakeRepo.LastSnapshotReceived); // Verifies snapshot was null (lock-free)
        Assert.Equal(1, fakeRepo.ReadCalls);
    }

    [Fact]
    public async Task MarkBackupCommandHandler_EnforcesBackupMarkScope_AndRejectsUnauthorizedCaller()
    {
        // Arrange
        var rejectingResolver = new RejectingScopeResolver("BACKUP_MARK");
        var handler = new MarkBackupCommandHandler(
            rejectingResolver,
            null!,
            NullLogger<MarkBackupCommandHandler>.Instance);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<StructureException>(() =>
            handler.Handle(new MarkBackupCommand(SiteId, SlotId, true, TenantId), CancellationToken.None));

        Assert.Equal("FORBIDDEN", ex.Code);
        Assert.Equal("BACKUP_MARK", rejectingResolver.LastPermissionChecked);
    }

    [Fact]
    public async Task BatchEditStructureCommandHandler_WhenBatchContainsMarkBackup_EnforcesBackupMarkScope()
    {
        // Arrange
        var rejectingResolver = new RejectingScopeResolver("BACKUP_MARK");
        var handler = new BatchEditStructureCommandHandler(
            rejectingResolver,
            null!,
            NullLogger<BatchEditStructureCommandHandler>.Instance);

        var items = new List<BatchItemBody>
        {
            new(SlotId, "markBackup")
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<StructureException>(() =>
            handler.Handle(new BatchEditStructureCommand(SiteId, Guid.NewGuid(), items, TenantId), CancellationToken.None));

        Assert.Equal("FORBIDDEN", ex.Code);
        Assert.Equal("BACKUP_MARK", rejectingResolver.LastPermissionChecked);
    }

    [Fact]
    public async Task BatchEditStructureCommandHandler_WhenBatchExceedsFiftyItems_ThrowsInvalidStructure()
    {
        // Arrange
        var rejectingResolver = new RejectingScopeResolver("BACKUP_MARK");
        var handler = new BatchEditStructureCommandHandler(
            rejectingResolver,
            null!,
            NullLogger<BatchEditStructureCommandHandler>.Instance);

        var items = Enumerable.Range(1, 51)
            .Select(_ => new BatchItemBody(Guid.NewGuid(), "removeSlot"))
            .ToList();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<StructureException>(() =>
            handler.Handle(new BatchEditStructureCommand(SiteId, Guid.NewGuid(), items, TenantId), CancellationToken.None));

        Assert.Equal("INVALID_STRUCTURE", ex.Code);
    }

    [Fact]
    public async Task ValidationBehavior_WhenValidationFails_ThrowsValidationException()
    {
        // Arrange
        var validator = new InlineTestValidator();
        var behavior = new ValidationBehavior<InlineTestCommand, Unit>([validator]);

        var command = new InlineTestCommand(Guid.Empty); // Empty Guid violates rule

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            behavior.Handle(command, _ => Task.FromResult(Unit.Value), CancellationToken.None));

        Assert.NotEmpty(ex.Errors);
    }

    private sealed record InlineTestCommand(Guid SiteId) : IRequest<Unit>;

    private sealed class InlineTestValidator : AbstractValidator<InlineTestCommand>
    {
        public InlineTestValidator()
        {
            RuleFor(x => x.SiteId).NotEmpty().WithMessage("SiteId cannot be empty.");
        }
    }

    private sealed class StubScopeResolver(OwnerScope scope) : IOwnerScopeResolver
    {
        public Task<OwnerScope> ResolveAsync(Guid? tenantId, CancellationToken ct, string permission = "PARKING_MANAGE")
            => Task.FromResult(scope);
    }

    private sealed class RejectingScopeResolver(string expectedPermission) : IOwnerScopeResolver
    {
        public string? LastPermissionChecked { get; private set; }

        public Task<OwnerScope> ResolveAsync(Guid? tenantId, CancellationToken ct, string permission = "PARKING_MANAGE")
        {
            LastPermissionChecked = permission;
            if (permission == expectedPermission)
                throw new StructureException("FORBIDDEN", $"Missing {permission} authority.");
            return Task.FromResult(Scope);
        }
    }

    private sealed class StubOwnerAuthorizer : IOwnerAuthorizer
    {
        public Task<bool> IsOwnerAsync(OwnerScope actor, CancellationToken ct) => Task.FromResult(true);
        public Task<bool> IsAuthorizedAsync(OwnerScope actor, Guid site, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class StubMaintenanceWindowStore : IMaintenanceWindowStore
    {
        public Task CancelAsync(OwnerScope owner, Guid siteId, Guid windowId, CancellationToken ct) => Task.CompletedTask;
        public Task<MaintenanceWindow> CreateAsync(OwnerScope owner, Guid siteId, DateTime start, DateTime end, string reason, CancellationToken ct) => throw new NotImplementedException();
        public Task<MaintenanceWindow> ExtendAsync(OwnerScope owner, Guid siteId, Guid windowId, DateTime? start, DateTime? end, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<MaintenanceWindow>> ListAsync(OwnerScope owner, Guid siteId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<MaintenanceWindow>>([]);
    }

    private sealed class StubReadRepository : IParkingStructureRepository
    {
        public CommitmentSnapshot? LastSnapshotReceived { get; private set; }
        public int ReadCalls { get; private set; }

        public Task<ParkingStructure> ReadAsync(OwnerScope owner, Guid siteId, CommitmentSnapshot? snapshot, CancellationToken ct)
        {
            ReadCalls++;
            LastSnapshotReceived = snapshot;
            var site = new SiteProfile(siteId, owner.TenantId, "SITE_01", "Site One", "Road 1", null, null, true, 10, "ACTIVE");
            return Task.FromResult(new ParkingStructure(site, [], [], [], null, false, null, null, [], [], [], null));
        }

        public Task<IParkingStructureEdit> BeginEditAsync(OwnerScope owner, Guid siteId, CommitmentSnapshot snapshot, Guid? operationId, CancellationToken ct) => throw new NotImplementedException();
        public Task<SiteProfile> CreateAsync(OwnerScope owner, SiteProfile site, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<SiteProfile>> ListAsync(OwnerScope owner, CancellationToken ct) => throw new NotImplementedException();
    }
}
