extern alias UserApplication;
using AccountAccessRequestV1 = UserApplication::SmartParking.Contracts.AccountAccess.AccountAccessRequestV1;
using AccountAccessDecisionV1 = UserApplication::SmartParking.Contracts.AccountAccess.AccountAccessDecisionV1;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Authorization;
using UserService.Application.Common.Interfaces.Persistence;

namespace SmartParking.UserService.Tests;

public sealed class AccountCurrentWorkflowTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 9, 0, 0, 0, TimeSpan.Zero);
    private static AccountAccessRequestV1 Request() => new(Guid.NewGuid(), Guid.NewGuid(), 1,
        Guid.NewGuid(), Guid.NewGuid(), "driver", "CREATE_RESERVATION", Start, Start.AddSeconds(5), Start.AddSeconds(90));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LaterDeliveryReplaysOriginalResultEvenWhenCurrentAuthorityChanges(bool originallyAllowed)
    {
        var clock = new Clock { Now = Start.AddSeconds(1) };
        var data = new Data { Allowed = originallyAllowed };
        var request = Request();
        await new EvaluateAccountCurrentHandler(data, data, data, data, clock)
            .Handle(new(request), CancellationToken.None);
        var original = Assert.Single(data.Replies);
        data.Allowed = !originallyAllowed;
        clock.Now = Start.AddSeconds(16);
        var retry = request with { AttemptId = Guid.NewGuid(), AttemptNumber = 2, AttemptDeadline = Start.AddSeconds(20) };
        // New handler instance simulates a request scope; persisted result is supplied by the repository.
        await new EvaluateAccountCurrentHandler(data, data, data, data, clock)
            .Handle(new(retry), CancellationToken.None);
        var replay = data.Replies[1];
        Assert.Equal(1, data.Reads);
        Assert.Equal(1, data.Adds);
        Assert.Equal(original.DecisionId, replay.DecisionId);
        Assert.Equal(original.DecidedAt, replay.DecidedAt);
        Assert.Equal(originallyAllowed, replay.Allowed);
        Assert.Equal(retry.AttemptId, replay.AttemptId);
        Assert.Equal(2, replay.AttemptNumber);
    }

    [Fact]
    public async Task ChangedActorUnderSameWorkflowIsRejectedWithoutEvaluation()
    {
        var clock = new Clock { Now = Start.AddSeconds(1) }; var data = new Data(); var request = Request();
        var handler = new EvaluateAccountCurrentHandler(data, data, data, data, clock);
        await handler.Handle(new(request), CancellationToken.None);
        await Assert.ThrowsAsync<ArgumentException>(() => handler.Handle(new(request with { AccountId = Guid.NewGuid() }), CancellationToken.None));
        Assert.Equal(1, data.Reads);
        Assert.Single(data.Replies);
    }

    [Fact]
    public async Task EvaluationFinishingAfterDeliveryTimeoutIsRetainedForLaterReplay()
    {
        var clock = new Clock { Now = Start.AddSeconds(1) }; var data = new Data { Allowed = true };
        data.AfterRead = () => clock.Now = Start.AddSeconds(6);
        var request = Request(); var handler = new EvaluateAccountCurrentHandler(data, data, data, data, clock);
        await handler.Handle(new(request), CancellationToken.None);
        Assert.NotNull(data.Stored);
        Assert.Empty(data.Replies);
        clock.Now = Start.AddSeconds(16);
        await handler.Handle(new(request with { AttemptId = Guid.NewGuid(), AttemptNumber = 2, AttemptDeadline = Start.AddSeconds(20) }), CancellationToken.None);
        Assert.Equal(1, data.Reads);
        Assert.Single(data.Replies);
    }

    [Fact]
    public async Task ExpiredDeliveryCannotEvaluateOrPublish()
    {
        var clock = new Clock { Now = Start.AddSeconds(90) }; var data = new Data();
        await new EvaluateAccountCurrentHandler(data, data, data, data, clock).Handle(new(Request()), CancellationToken.None);
        Assert.Equal(0, data.Reads);
        Assert.Empty(data.Replies);
        Assert.Null(data.Stored);
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    // Data boundaries only; locking/commit behavior is separately verified in deployment PostgreSQL.
    private sealed class Data : IAccountAuthorityRepository, IAccountAccessDecisionRepository,
        IAccountAccessDecisionPublisher, IUnitOfWork, IUnitOfWorkTransaction
    {
        public bool Allowed;
        public int Reads, Adds;
        public Action? AfterRead;
        public AccountAccessDecision? Stored;
        public List<AccountAccessDecisionV1> Replies { get; } = [];
        public Task<CurrentAccountAuthority?> ReadAsync(Guid user, Guid account, DateTimeOffset now, CancellationToken ct)
        {
            Reads++; AfterRead?.Invoke();
            return Task.FromResult<CurrentAccountAuthority?>(new(Allowed, Allowed, Allowed, null));
        }
        public Task LockWorkflowAsync(Guid workflow, CancellationToken ct) => Task.CompletedTask;
        public Task<AccountAccessDecision?> FindAsync(Guid workflow, CancellationToken ct) => Task.FromResult(Stored);
        public void Add(AccountAccessDecision decision) { Adds++; Stored = decision; }
        public Task SendAsync(AccountAccessDecisionV1 decision, CancellationToken ct) { Replies.Add(decision); return Task.CompletedTask; }
        public IUserRepository UserRepository => throw new NotSupportedException();
        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct) => Task.FromResult<IUnitOfWorkTransaction>(this);
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
        public Task CommitAsync(CancellationToken ct) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
