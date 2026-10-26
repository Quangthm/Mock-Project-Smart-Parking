using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.JwT;
using UserService.Application.Services;
using UserService.Application.Usecase.Login;
using UserService.Infrastructure.Services;

namespace SmartParking.UserService.Tests;

internal sealed class AuthTestFixture : IUserRepository, IUnitOfWork, IProfileAccessStore
{
    private static readonly string PasswordHash = new BcryptPasswordService().Hash("Password@123");
    public TestClock Clock { get; } = new();
    public MemorySessions Sessions { get; }
    public User User { get; } = new()
    {
        Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Email = "driver@gmail.com", FullName = "Demo Driver",
        PasswordHash = PasswordHash, Status = UserStatus.Active,
        Accounts = [new Account { AccountRoles = [new AccountRole { RoleCode = "DRIVER" }] }]
    };
    public AuthenticationPolicy Policy { get; } = new();
    public JwtOptions Options { get; } = new() { Issuer = "test-issuer", Audience = "test-audience" };
    public JwtKeyProvider Keys { get; }
    public AccessTokenService Tokens { get; }
    public AuthSessionService Auth { get; }
    public LoginCommandHandler Login { get; }
    public int Saved { get; private set; }
    public bool FailSave { get; set; }
    public IUserRepository UserRepository => this;
    public AuthTestFixture()
    {
        foreach (var account in User.Accounts)
        {
            account.Id = Guid.Parse("22222222-2222-2222-2222-222222222222");
            account.UserId = User.Id!.Value;
            account.User = User;
            foreach (var role in account.AccountRoles) role.AccountId = account.Id.Value;
        }
        Sessions = new(Clock);
        Keys = new(Options, true);
        Tokens = new(Options, Keys, Sessions, Clock);
        Auth = new(Tokens, Sessions, Policy, Clock);
        Login = new(this, new BcryptPasswordService(), Auth, Policy, Clock);
    }
    public Task<ProfileAccessState?> LoadCurrentAsync(Guid accountId, Guid accessTokenId, CancellationToken cancellationToken)
    {
        var account = User.Accounts.SingleOrDefault(a => a.Id == accountId);
        var session = Sessions.Snapshot.SingleOrDefault(s => s.AccessTokenId == accessTokenId);
        return Task.FromResult(account is null || session is null ? null
            : new ProfileAccessState(User, account, session, account.AccountRoles.Select(r => r.RoleCode).ToArray()));
    }
    public Task<ProfileAccessState?> LockCurrentAsync(Guid accountId, Guid accessTokenId, CancellationToken cancellationToken) =>
        LoadCurrentAsync(accountId, accessTokenId, cancellationToken);
    public Task<LoginResult> SignIn(string password = "Password@123", string email = "driver@gmail.com") =>
        Login.Handle(new LoginCommand { Email = email, Password = password }, default);
    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken) => Task.FromResult(email == User.Email ? User : null);
    public Task<User?> GetByEmailForLoginAsync(string email, CancellationToken cancellationToken) => GetByEmailAsync(email, cancellationToken);
    public Task<User?> GetByIdWithRolesAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(id == User.Id ? User : null);
    public Task<User?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(id == User.Id ? User : null);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (FailSave) throw new InvalidOperationException("Simulated database outage");
        Saved++; return Task.FromResult(1);
    }
    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken) => Task.FromResult<IUnitOfWorkTransaction>(new Transaction());
    private sealed class Transaction : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    public Task AddAsync(User entity, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task AddRangeAsync(List<User> entities, CancellationToken cancellationToken) => Task.CompletedTask;
    public IQueryable<User> Query() => new[] { User }.AsQueryable();
    public IQueryable<User> QueryIncludingDeleted() => Query();
    public void Remove(User entity) { }
    public void Update(User entity) { }
}

internal sealed class TestCurrentActor(Guid? userId, string? role = null, Guid? accountId = null, Guid? accessTokenId = null) : ICurrentActor
{
    public Guid? UserId { get; } = userId;
    public string? Role { get; } = role;
    public Guid? AccountId { get; } = accountId;
    public Guid? AccessTokenId { get; } = accessTokenId;
}

internal sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(DateTimeOffset.UtcNow.Ticks / 10 * 10, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

// Test adapter only. Production DI always resolves PostgresAuthSessionStore.
internal sealed class MemorySessions(TestClock clock) : IAuthSessionStore
{
    private readonly Dictionary<Guid, AuthSession> items = [];
    private readonly object gate = new();
    public bool FailRevoke { get; set; }
    public IReadOnlyList<AuthSession> Snapshot { get { lock (gate) return items.Values.Select(Clone).ToArray(); } }
    public Task AddAsync(AuthSession session, CancellationToken cancellationToken)
    {
        lock (gate) items.Add(session.Id, Clone(session));
        return Task.CompletedTask;
    }
    public Task<bool> IsActiveAsync(Guid id, Guid user, CancellationToken cancellationToken)
    {
        lock (gate) return Task.FromResult(items.Values.Any(s => s.AccessTokenId == id && s.UserId == user
            && s.AccountId != null && !s.IsRevoked && s.AccessExpiresAt > clock.Now && s.ExpiresAt > clock.Now));
    }
    public Task<bool> IsActiveAsync(Guid id, Guid user, Guid account, CancellationToken cancellationToken)
    {
        lock (gate) return Task.FromResult(items.Values.Any(s => s.AccessTokenId == id && s.UserId == user
            && s.AccountId == account && !s.IsRevoked && s.AccessExpiresAt > clock.Now && s.ExpiresAt > clock.Now));
    }
    public Task<AuthSession?> FindByRefreshHashAsync(string hash, CancellationToken cancellationToken)
    {
        lock (gate) return Task.FromResult(items.Values.FirstOrDefault(s => s.TokenHash == hash) is { } s ? Clone(s) : null);
    }
    public Task<bool> RotateAsync(AuthSession old, Guid id, string hash, DateTimeOffset expires, CancellationToken cancellationToken)
    {
        lock (gate)
        {
            if (!items.TryGetValue(old.Id, out var s) || s.IsRevoked || s.ExpiresAt <= clock.Now
                || s.TokenHash != old.TokenHash || s.AccessTokenId != old.AccessTokenId || s.UserId != old.UserId)
                return Task.FromResult(false);
            if (s.AccountId is null || s.AccountId != old.AccountId) return Task.FromResult(false);
            s.AccessTokenId = id; s.TokenHash = hash; s.AccessExpiresAt = expires;
            return Task.FromResult(true);
        }
    }
    public Task<bool> RevokeAsync(Guid id, Guid user, string hash, CancellationToken cancellationToken)
    {
        if (FailRevoke) throw new InvalidOperationException("Simulated database outage");
        lock (gate)
        {
            var s = items.Values.FirstOrDefault(s => s.AccessTokenId == id && s.UserId == user && s.TokenHash == hash
                && !s.IsRevoked && s.AccessExpiresAt > clock.Now && s.ExpiresAt > clock.Now);
            if (s is null) return Task.FromResult(false);
            s.IsRevoked = true;
            return Task.FromResult(true);
        }
    }
    private static AuthSession Clone(AuthSession s) => new()
    {
        Id = s.Id, UserId = s.UserId, AccountId = s.AccountId, AccessTokenId = s.AccessTokenId, TokenHash = s.TokenHash,
        AccessExpiresAt = s.AccessExpiresAt, ExpiresAt = s.ExpiresAt, CreatedAt = s.CreatedAt, IsRevoked = s.IsRevoked
    };
}
