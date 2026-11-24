using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.FacilityService.API.Middleware;
using SmartParking.FacilityService.Application.Common.Interfaces.Services;
using SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.CancelMaintenanceWindow;
using SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.ExtendMaintenanceWindow;
using SmartParking.FacilityService.Application.Usecases.Maintenance.Commands.ScheduleMaintenanceWindow;
using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Infrastructure;

namespace SmartParking.FacilityService.Tests;

// Two-phase maintenance sync (DE-13): the local row is always committed first and the row only
// becomes `acknowledged` after Operations names the enforcement. A refusal must surface
// STRUCTURE_IN_USE with the overlapping commitments and must never leave the window in force.
public sealed class MaintenanceEnforcementFlowTests
{
    private static readonly OwnerScope Owner = new(Guid.NewGuid(), Guid.NewGuid());
    private static readonly Guid SiteId = Guid.NewGuid();
    private static readonly Guid EnforcementRef = Guid.NewGuid();

    private static ScheduleMaintenanceWindowCommandHandler Schedule(
        FakeWindowStore store, IMaintenanceEnforcementClient client)
        => new(new StubScopeResolver(), new StubOwnerAuthorizer(), store,
            NullLogger<ScheduleMaintenanceWindowCommandHandler>.Instance, client);

    private static ExtendMaintenanceWindowCommandHandler Extend(
        FakeWindowStore store, IMaintenanceEnforcementClient client)
        => new(new StubScopeResolver(), new StubOwnerAuthorizer(), store,
            NullLogger<ExtendMaintenanceWindowCommandHandler>.Instance, client);

    private static CancelMaintenanceWindowCommandHandler Cancel(
        FakeWindowStore store, IMaintenanceEnforcementClient client)
        => new(new StubScopeResolver(), new StubOwnerAuthorizer(), store,
            NullLogger<CancelMaintenanceWindowCommandHandler>.Instance, client);

    private static ScheduleMaintenanceWindowCommand NewWindowCommand()
        => new(SiteId, DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(3), "Repaving");

    private static MaintenanceWindow ExistingWindow(string state = "acknowledged", Guid? reference = null)
    {
        var start = DateTime.UtcNow.AddHours(1);
        return new MaintenanceWindow(Guid.NewGuid(), Owner.TenantId, SiteId, null, null,
            start, start.AddHours(1), "Repaving", Owner.UserId, start.AddDays(-1), null,
            state, reference, reference is null ? null : start.AddDays(-1));
    }

    [Fact]
    public async Task Schedule_Acknowledged_MarksWindowAcknowledgedWithEnforcementRef()
    {
        var store = new FakeWindowStore();
        var client = new FakeEnforcementClient(
            new MaintenanceEnforcementResult.Acknowledged(EnforcementRef, DateTime.UtcNow));

        var window = await Schedule(store, client).Handle(NewWindowCommand(), CancellationToken.None);

        Assert.Equal(1, store.CreateCalls);
        Assert.Equal(1, store.MarkAcknowledgedCalls);
        Assert.Equal("acknowledged", window.EnforcementState);
        Assert.Equal(EnforcementRef, window.EnforcementRef);
        Assert.NotNull(window.AcknowledgedAt);
        Assert.Equal(0, store.DeletePendingCalls);
    }

    [Fact]
    public async Task Schedule_Conflict_DropsPendingWindowAndRethrowsCommitments()
    {
        var store = new FakeWindowStore();
        var commitment = new MaintenanceCommitmentItem(Guid.NewGuid(), "RESERVATION",
            DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), null, Guid.NewGuid());
        var client = new FakeEnforcementClient(
            new MaintenanceEnforcementResult.Conflict([commitment], "1 commitment(s) overlap the requested maintenance window."));

        var error = await Assert.ThrowsAsync<MaintenanceConflictException>(() =>
            Schedule(store, client).Handle(NewWindowCommand(), CancellationToken.None));

        Assert.Equal("STRUCTURE_IN_USE", error.Code);
        Assert.Equal(commitment, Assert.Single(error.Commitments));
        Assert.Equal(1, store.DeletePendingCalls);
        Assert.NotNull(store.Current?.CancelledAt);
        Assert.Equal(0, store.MarkAcknowledgedCalls);
    }

    [Fact]
    public async Task Schedule_Unavailable_LeavesWindowPendingForTheRetryWorker()
    {
        var store = new FakeWindowStore();
        var client = new FakeEnforcementClient(new MaintenanceEnforcementResult.Unavailable());

        var window = await Schedule(store, client).Handle(NewWindowCommand(), CancellationToken.None);

        Assert.Equal("pending", window.EnforcementState);
        Assert.Null(window.EnforcementRef);
        Assert.Equal(0, store.MarkAcknowledgedCalls);
        Assert.Equal(0, store.DeletePendingCalls);
    }

    [Fact]
    public async Task Extend_Acknowledged_ReturnsExtendedAcknowledgedWindow()
    {
        var current = ExistingWindow(reference: Guid.NewGuid());
        var store = new FakeWindowStore { Current = current };
        var newEnd = current.End.AddHours(1);
        var client = new FakeEnforcementClient(
            new MaintenanceEnforcementResult.Acknowledged(EnforcementRef, DateTime.UtcNow));

        var window = await Extend(store, client).Handle(
            new ExtendMaintenanceWindowCommand(SiteId, current.Id, null, newEnd), CancellationToken.None);

        Assert.Equal(newEnd, window.End);
        Assert.Equal("acknowledged", window.EnforcementState);
        Assert.Equal(EnforcementRef, window.EnforcementRef);
        Assert.Equal(0, store.RevertExtensionCalls);
    }

    [Fact]
    public async Task Extend_Conflict_RestoresPreviousWindowAndRethrowsCommitments()
    {
        var previousRef = Guid.NewGuid();
        var current = ExistingWindow(reference: previousRef);
        var store = new FakeWindowStore { Current = current };
        var commitment = new MaintenanceCommitmentItem(Guid.NewGuid(), "SESSION",
            DateTime.UtcNow.AddHours(1), null, null, null);
        var client = new FakeEnforcementClient(
            new MaintenanceEnforcementResult.Conflict([commitment], "1 commitment(s) overlap the requested maintenance window."));

        var error = await Assert.ThrowsAsync<MaintenanceConflictException>(() =>
            Extend(store, client).Handle(
                new ExtendMaintenanceWindowCommand(SiteId, current.Id, null, current.End.AddHours(1)),
                CancellationToken.None));

        Assert.Equal("STRUCTURE_IN_USE", error.Code);
        Assert.Equal(commitment, Assert.Single(error.Commitments));
        Assert.Equal(1, store.RevertExtensionCalls);
        Assert.Equal(current.End, store.RevertedEnd);
        Assert.Equal("acknowledged", store.RevertedEnforcementState);
        Assert.Equal(previousRef, store.RevertedEnforcementRef);
        // The stored row is back to the acknowledged interval, never the rejected wider one.
        Assert.Equal(current.End, store.Current!.End);
        Assert.Equal("acknowledged", store.Current.EnforcementState);
    }

    [Fact]
    public async Task Cancel_RemoteReleaseSucceedsAndClearsEnforcementRef()
    {
        var current = ExistingWindow(reference: Guid.NewGuid());
        var store = new FakeWindowStore { Current = current };
        var client = new FakeEnforcementClient(new MaintenanceEnforcementResult.Unavailable()) { CancelResult = true };

        await Cancel(store, client).Handle(
            new CancelMaintenanceWindowCommand(SiteId, current.Id), CancellationToken.None);

        Assert.Equal(1, store.CancelCalls);
        Assert.True(client.CancelCalled);
        Assert.Equal(1, store.ClearEnforcementRefCalls);
        Assert.Null(store.Current!.EnforcementRef);
        Assert.NotNull(store.Current.CancelledAt);
    }

    [Fact]
    public async Task Cancel_RemoteReleaseFails_KeepsEnforcementRefForLaterSync()
    {
        var reference = Guid.NewGuid();
        var current = ExistingWindow(reference: reference);
        var store = new FakeWindowStore { Current = current };
        var client = new FakeEnforcementClient(new MaintenanceEnforcementResult.Unavailable()) { CancelResult = false };

        await Cancel(store, client).Handle(
            new CancelMaintenanceWindowCommand(SiteId, current.Id), CancellationToken.None);

        Assert.True(client.CancelCalled);
        Assert.Equal(0, store.ClearEnforcementRefCalls);
        Assert.Equal(reference, store.Current!.EnforcementRef);
    }

    [Fact]
    public async Task ApplyAsync_Success_PutsWindowAndParsesAcknowledgement()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            $$"""{"enforcementRef":"{{EnforcementRef}}","acknowledgedAt":"2026-11-22T10:05:00Z"}""",
            "application/json");
        var client = new MaintenanceEnforcementClient(Client(handler));
        var window = ExistingWindow(state: "pending");

        var result = await client.ApplyAsync(window,
            new MaintenanceWindowScope([], []), CancellationToken.None);

        var ack = Assert.IsType<MaintenanceEnforcementResult.Acknowledged>(result);
        Assert.Equal(EnforcementRef, ack.EnforcementRef);
        Assert.Equal(new DateTime(2026, 11, 22, 10, 5, 0, DateTimeKind.Utc), ack.AcknowledgedAt);
        Assert.Equal(HttpMethod.Put, handler.LastMethod);
        Assert.Equal($"/internal/structure/{window.SiteId}/maintenance-windows/{window.Id}", handler.LastPath);
    }

    [Fact]
    public async Task ApplyAsync_Conflict_ParsesCommitmentsAndMessage()
    {
        var commitmentId = Guid.NewGuid();
        var slotId = Guid.NewGuid();
        var handler = new StubHandler(HttpStatusCode.Conflict,
            $$"""
            {"success":false,"code":"STRUCTURE_IN_USE","message":"1 commitment(s) overlap the requested maintenance window.",
             "commitments":[{"id":"{{commitmentId}}","type":"RESERVATION","start":"2026-11-22T09:00:00Z",
             "end":"2026-11-22T11:00:00Z","unitId":null,"slotId":"{{slotId}}"}]}
            """,
            "application/json");
        var client = new MaintenanceEnforcementClient(Client(handler));

        var result = await client.ApplyAsync(ExistingWindow(state: "pending"),
            new MaintenanceWindowScope([], []), CancellationToken.None);

        var conflict = Assert.IsType<MaintenanceEnforcementResult.Conflict>(result);
        Assert.Equal("1 commitment(s) overlap the requested maintenance window.", conflict.Message);
        var commitment = Assert.Single(conflict.Commitments);
        Assert.Equal(commitmentId, commitment.Id);
        Assert.Equal("RESERVATION", commitment.Type);
        Assert.Equal(slotId, commitment.SlotId);
        Assert.Null(commitment.UnitId);
    }

    [Fact]
    public async Task ApplyAsync_TransientConflictCode_ReturnsUnavailableInsteadOfConflict()
    {
        var logger = new CapturingLogger<MaintenanceEnforcementClient>();
        var client = new MaintenanceEnforcementClient(
            Client(new StubHandler(HttpStatusCode.Conflict,
                """{"success":false,"code":"STRUCTURE_BUSY","message":"Another structure edit is in progress."}""",
                "application/json")),
            logger);

        var result = await client.ApplyAsync(ExistingWindow(state: "pending"),
            new MaintenanceWindowScope([], []), CancellationToken.None);

        Assert.IsType<MaintenanceEnforcementResult.Unavailable>(result);
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Error && entry.Message.Contains("409"));
    }

    [Fact]
    public async Task ApplyAsync_ServerError_ReturnsUnavailableAndLogsStatusCode()
    {
        var logger = new CapturingLogger<MaintenanceEnforcementClient>();
        var client = new MaintenanceEnforcementClient(
            Client(new StubHandler(HttpStatusCode.InternalServerError, "boom", "text/plain")), logger);

        var result = await client.ApplyAsync(ExistingWindow(state: "pending"),
            new MaintenanceWindowScope([], []), CancellationToken.None);

        Assert.IsType<MaintenanceEnforcementResult.Unavailable>(result);
        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Error && entry.Message.Contains("500"));
    }

    [Fact]
    public async Task CancelAsync_NotFound_CountsAsReleased()
    {
        var client = new MaintenanceEnforcementClient(
            Client(new StubHandler(HttpStatusCode.NotFound, null, "application/json")));

        Assert.True(await client.CancelAsync(Owner.TenantId, SiteId, Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ExceptionHandler_MapsCommitmentConflictToStructureInUseWithCommitments()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = new MemoryStream();
        var slotId = Guid.NewGuid();
        var commitment = new MaintenanceCommitmentItem(Guid.NewGuid(), "RESERVATION",
            DateTimeOffset.Parse("2026-11-22T09:00:00Z"), DateTimeOffset.Parse("2026-11-22T11:00:00Z"),
            null, slotId);
        var handler = new FacilityExceptionHandler(NullLogger<FacilityExceptionHandler>.Instance);

        var handled = await handler.TryHandleAsync(context,
            new MaintenanceConflictException([commitment], "1 commitment(s) overlap the requested maintenance window."),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(409, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        var root = document.RootElement;
        Assert.Equal("STRUCTURE_IN_USE", root.GetProperty("code").GetString());
        Assert.Equal("1 commitment(s) overlap the requested maintenance window.", root.GetProperty("message").GetString());
        var commitments = root.GetProperty("commitments");
        Assert.Equal(1, commitments.GetArrayLength());
        Assert.Equal("RESERVATION", commitments[0].GetProperty("type").GetString());
        Assert.Equal(slotId, commitments[0].GetProperty("slotId").GetGuid());
        Assert.Equal(JsonValueKind.Null, commitments[0].GetProperty("unitId").ValueKind);
    }

    private static HttpClient Client(StubHandler handler)
        => new(handler) { BaseAddress = new Uri("http://localhost/") };

    private sealed class StubHandler(HttpStatusCode status, string? body, string contentType) : HttpMessageHandler
    {
        public HttpMethod? LastMethod { get; private set; }
        public string? LastPath { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastPath = request.RequestUri?.AbsolutePath;
            var response = new HttpResponseMessage(status);
            if (body is not null)
                response.Content = new StringContent(body, Encoding.UTF8, contentType);
            return Task.FromResult(response);
        }
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

    private sealed class StubScopeResolver : IOwnerScopeResolver
    {
        public Task<OwnerScope> ResolveAsync(Guid? tenantId, CancellationToken ct, string permission = "PARKING_MANAGE")
            => Task.FromResult(Owner);
    }

    private sealed class StubOwnerAuthorizer : IOwnerAuthorizer
    {
        public Task<bool> IsOwnerAsync(OwnerScope actor, CancellationToken ct) => Task.FromResult(true);
        public Task<bool> IsAuthorizedAsync(OwnerScope actor, Guid site, CancellationToken ct) => Task.FromResult(true);
    }

    private sealed class FakeEnforcementClient(MaintenanceEnforcementResult result) : IMaintenanceEnforcementClient
    {
        public bool CancelResult { get; init; } = true;
        public bool CancelCalled { get; private set; }

        public Task<MaintenanceEnforcementResult> ApplyAsync(
            MaintenanceWindow window, MaintenanceWindowScope scope, CancellationToken ct)
            => Task.FromResult(result);

        public Task<bool> CancelAsync(Guid tenantId, Guid siteId, Guid windowId, CancellationToken ct)
        {
            CancelCalled = true;
            return Task.FromResult(CancelResult);
        }
    }

    private sealed class FakeWindowStore : IMaintenanceWindowStore
    {
        public MaintenanceWindow? Current { get; set; }
        public int CreateCalls { get; private set; }
        public int MarkAcknowledgedCalls { get; private set; }
        public int DeletePendingCalls { get; private set; }
        public int RevertExtensionCalls { get; private set; }
        public int ClearEnforcementRefCalls { get; private set; }
        public int CancelCalls { get; private set; }
        public DateTime? RevertedEnd { get; private set; }
        public string? RevertedEnforcementState { get; private set; }
        public Guid? RevertedEnforcementRef { get; private set; }

        public Task<IReadOnlyList<MaintenanceWindow>> ListAsync(
            OwnerScope owner, Guid siteId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<MaintenanceWindow>>(Current is null ? [] : [Current]);

        public Task<MaintenanceWindow?> GetByIdAsync(
            OwnerScope owner, Guid siteId, Guid windowId, CancellationToken ct)
            => Task.FromResult(Current);

        public Task<MaintenanceWindow> CreateAsync(OwnerScope owner, Guid siteId, DateTime start, DateTime end,
            string reason, CancellationToken ct, Guid? unitId = null, Guid? slotId = null)
        {
            CreateCalls++;
            Current = new MaintenanceWindow(Guid.NewGuid(), owner.TenantId, siteId, unitId, slotId,
                start, end, reason, owner.UserId, DateTime.UtcNow, null, "pending", null, null);
            return Task.FromResult(Current);
        }

        public Task<MaintenanceWindow> ExtendAsync(
            OwnerScope owner, Guid siteId, Guid windowId, DateTime newEnd, CancellationToken ct)
        {
            Current = Current! with
            {
                End = newEnd, EnforcementState = "pending", EnforcementRef = null, AcknowledgedAt = null
            };
            return Task.FromResult(Current);
        }

        public Task CancelAsync(OwnerScope owner, Guid siteId, Guid windowId, CancellationToken ct)
        {
            CancelCalls++;
            Current = Current! with { CancelledAt = DateTime.UtcNow };
            return Task.CompletedTask;
        }

        public Task<MaintenanceWindowScope> ResolveScopeAsync(
            Guid tenantId, Guid siteId, Guid? unitId, Guid? slotId, CancellationToken ct)
            => Task.FromResult(new MaintenanceWindowScope(
                unitId is { } unit ? [unit] : [], slotId is { } slot ? [slot] : []));

        public Task<MaintenanceWindow> MarkAcknowledgedAsync(Guid tenantId, Guid siteId, Guid windowId,
            Guid enforcementRef, DateTime acknowledgedAt, CancellationToken ct)
        {
            MarkAcknowledgedCalls++;
            Current = Current! with
            {
                EnforcementState = "acknowledged", EnforcementRef = enforcementRef, AcknowledgedAt = acknowledgedAt
            };
            return Task.FromResult(Current);
        }

        public Task MarkFailedAsync(Guid tenantId, Guid siteId, Guid windowId, CancellationToken ct)
        {
            Current = Current! with { EnforcementState = "failed" };
            return Task.CompletedTask;
        }

        public Task DeletePendingAsync(Guid tenantId, Guid siteId, Guid windowId, CancellationToken ct)
        {
            DeletePendingCalls++;
            Current = Current! with { CancelledAt = DateTime.UtcNow };
            return Task.CompletedTask;
        }

        public Task<MaintenanceWindow> RevertExtensionAsync(Guid tenantId, Guid siteId, Guid windowId,
            DateTime previousEnd, string previousEnforcementState, Guid? previousEnforcementRef,
            DateTime? previousAcknowledgedAt, CancellationToken ct)
        {
            RevertExtensionCalls++;
            RevertedEnd = previousEnd;
            RevertedEnforcementState = previousEnforcementState;
            RevertedEnforcementRef = previousEnforcementRef;
            Current = Current! with
            {
                End = previousEnd,
                EnforcementState = previousEnforcementState,
                EnforcementRef = previousEnforcementRef,
                AcknowledgedAt = previousAcknowledgedAt
            };
            return Task.FromResult(Current);
        }

        public Task ClearEnforcementRefAsync(Guid tenantId, Guid siteId, Guid windowId, CancellationToken ct)
        {
            ClearEnforcementRefCalls++;
            Current = Current! with { EnforcementRef = null };
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MaintenanceWindow>> ListPendingEnforcementAsync(DateTime now, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<MaintenanceWindow>>([]);

        public Task<IReadOnlyList<MaintenanceWindow>> ListCancelledPendingRemoteSyncAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<MaintenanceWindow>>([]);

        public Task<int> NotifyDueBoundariesAsync(DateTime now, CancellationToken ct) => Task.FromResult(0);
    }
}