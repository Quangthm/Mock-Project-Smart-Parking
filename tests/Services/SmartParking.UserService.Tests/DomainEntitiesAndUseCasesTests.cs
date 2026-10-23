using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.Application.Common.Interfaces.Grpc;
using UserService.Application.Usecase.ValidateAccessToken;

namespace SmartParking.UserService.Tests;

public sealed class DomainEntitiesAndUseCasesTests
{
    [Fact]
    public void UserAndAccountEntities_SetAndGetProperties()
    {
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var user = new User
        {
            Id = userId,
            Phone = "0987654321",
            Email = "driver@smartpark.local",
            PasswordHash = "hash123",
            FullName = "Driver Demo",
            CompanyName = "SmartPark Corp",
            EmailVerifiedAt = now,
            PhoneVerifiedAt = now,
            Status = UserStatus.Active,
            FailedLoginAttempts = 0,
            LockedUntil = null,
            CreatedOn = now,
            ModifiedOn = now
        };

        var account = new Account
        {
            Id = accountId,
            UserId = userId,
            TenantId = tenantId,
            SiteId = siteId,
            Status = "ACTIVE",
            Permissions = ["PARKING:READ", "PARKING:WRITE"],
            User = user,
            CreatedOn = now,
            ModifiedOn = now
        };

        user.Accounts.Add(account);

        Assert.Equal(userId, user.Id);
        Assert.Equal("driver@smartpark.local", user.Email);
        Assert.Equal("0987654321", user.Phone);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Single(user.Accounts);
        Assert.Equal(accountId, account.Id);
        Assert.Equal(tenantId, account.TenantId);
        Assert.Equal(siteId, account.SiteId);
        Assert.Equal("ACTIVE", account.Status);
        Assert.Equal(2, account.Permissions!.Length);
    }

    [Fact]
    public void RoleAndAccountRole_SetAndGetProperties()
    {
        var role = new Role
        {
            Code = "DRIVER",
            Name = "Driver Role",
            Description = "Standard parking user"
        };

        var accountRole = new AccountRole
        {
            AccountId = Guid.NewGuid(),
            RoleCode = "DRIVER",
            Role = role
        };

        role.AccountRoles.Add(accountRole);

        Assert.Equal("DRIVER", role.Code);
        Assert.Equal("Driver Role", role.Name);
        Assert.Equal("Standard parking user", role.Description);
        Assert.Single(role.AccountRoles);
        Assert.Equal("DRIVER", accountRole.RoleCode);
    }

    [Fact]
    public void OperatorGrantAndOwnerApplication_Properties()
    {
        var accountId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var grant = new OperatorGrant
        {
            AccountId = accountId,
            CreatedBy = userId,
            CreatedAt = now,
            Permissions = ["OPERATOR:GATE"]
        };

        Assert.Equal(accountId, grant.AccountId);
        Assert.Equal(userId, grant.CreatedBy);
        Assert.Contains("OPERATOR:GATE", grant.Permissions);

        var app = new OwnerApplication
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BusinessName = "Downtown Parking Co",
            LotType = "commercial",
            Status = "pending",
            SubmittedAt = now,
            ReviewNote = "Under review",
            ReviewedAt = now.AddDays(1),
            ReviewedBy = Guid.NewGuid()
        };

        Assert.Equal("Downtown Parking Co", app.BusinessName);
        Assert.Equal("commercial", app.LotType);
        Assert.Equal("pending", app.Status);
    }

    [Fact]
    public void WorkflowEntities_SetAndGetProperties()
    {
        var now = DateTimeOffset.UtcNow;
        var challengeId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var challenge = new AuthChallenge
        {
            Id = challengeId,
            UserId = userId,
            Purpose = "login",
            Channel = "sms",
            Destination = "0987654321",
            CodeHash = "code123",
            ExpiresAt = now.AddMinutes(5),
            ResendAt = now.AddMinutes(1),
            Attempts = 1,
            LockedUntil = null,
            ConsumedAt = now
        };

        Assert.Equal(challengeId, challenge.Id);
        Assert.Equal("login", challenge.Purpose);
        Assert.Equal("sms", challenge.Channel);

        var delivery = new WorkflowDelivery
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ActorId = userId,
            Kind = "otp_email",
            ProtectedPayload = "payload",
            Status = "sent",
            Attempts = 1,
            CreatedAt = now,
            SentAt = now,
            RetryAt = null,
            ChallengeId = challengeId,
            ExpiresAt = now.AddHours(1)
        };

        Assert.Equal("otp_email", delivery.Kind);
        Assert.Equal("sent", delivery.Status);

        var mfa = new MfaCredential
        {
            UserId = userId,
            ProtectedSecret = "secret",
            Enabled = true,
            LastStep = 100,
            Attempts = 0,
            LockedUntil = null
        };

        Assert.True(mfa.Enabled);
        Assert.Equal(100, mfa.LastStep);

        var vehicle = new RegisteredVehicle
        {
            Id = Guid.NewGuid(),
            AccountId = Guid.NewGuid(),
            RawPlate = "29A-12345",
            CanonicalPlate = "29A12345",
            VehicleType = "CAR",
            ImageReference = "http://storage/car.jpg",
            CreatedAt = now,
            UpdatedAt = now,
            DeletedAt = null
        };

        Assert.Equal("29A12345", vehicle.CanonicalPlate);
        Assert.Equal("CAR", vehicle.VehicleType);

        var reg = new DriverRegistration
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Channel = "email",
            CodeHash = "code123",
            ExpiresAt = now.AddMinutes(15),
            ResendAvailableAt = now.AddMinutes(1),
            FailedAttempts = 0,
            LockedUntil = null,
            VerifiedAt = now
        };

        Assert.Equal("email", reg.Channel);
        Assert.Equal(0, reg.FailedAttempts);
    }

    [Fact]
    public async Task ValidateAccessTokenCommandHandler_ReturnsTrue()
    {
        var grpc = new FakeGrpc();
        var handler = new ValidateAccessTokenCommandHandler(grpc);

        var result = await handler.Handle(new ValidateAccessTokenCommand { AccessToken = "mock-jwt-token" }, CancellationToken.None);

        Assert.True(result);
        Assert.True(grpc.Client.WasCalled);
    }

    private sealed class FakeGrpc : IUnitOfGrpc
    {
        public FakeGrpcClient Client { get; } = new();
        public IDemoGrpcClient DemoGrpcClient => Client;
    }

    private sealed class FakeGrpcClient : IDemoGrpcClient
    {
        public bool WasCalled { get; private set; }
        public Task<bool> CheckLoginAsync(ValidateAccessTokenCommand command, CancellationToken cancellationToken)
        {
            WasCalled = true;
            return Task.FromResult(true);
        }
    }
}
