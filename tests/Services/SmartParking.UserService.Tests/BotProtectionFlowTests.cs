using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartParking.UserService.Domain.Entities;
using SmartParking.UserService.Domain.Enum;
using UserService.API.Controllers;
using UserService.API.Extensions;
using UserService.Application;
using UserService.Application.Common.Interfaces.MessageBroker;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;
using UserService.Application.Common.Models.JwT;
using UserService.Application.DTOs;
using UserService.Application.Services;
using UserService.Application.Usecase.Login;
using UserService.Infrastructure.Services;

namespace SmartParking.UserService.Tests;

public sealed class BotProtectionFlowTests
{
    private sealed class MockBotVerificationService : IBotVerificationService
    {
        public string? LastToken { get; private set; }
        public string? LastAction { get; private set; }
        public string? LastRemoteIp { get; private set; }
        public int CallCount { get; private set; }
        public Exception? ExceptionToThrow { get; set; }

        public Task VerifyAsync(string? token, string action, CancellationToken ct, string? remoteIp = null)
        {
            CallCount++;
            LastToken = token;
            LastAction = action;
            LastRemoteIp = remoteIp;

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                throw new AuthException("BOT_VERIFICATION_REQUIRED", "Bot verification token is required.", 400);
            }

            if (token == "invalid-token")
            {
                throw new AuthException("BOT_VERIFICATION_FAILED", "Bot verification failed.", 400);
            }

            return Task.CompletedTask;
        }
    }

    public class MockStoreProxy : DispatchProxy
    {
        public int BeginTransactionCalls;
        public int StageCalls;
        public int SaveCalls;

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod!.Name switch
        {
            "BeginTransactionAsync" => BeginTransaction(),
            "Stage" => Stage(),
            "SaveRegistrationAsync" => Task.FromResult(1),
            "ContactExistsAsync" => Task.FromResult(false),
            "OwnerContactExistsAsync" => Task.FromResult(false),
            _ => throw new NotSupportedException($"Method {targetMethod.Name} is not supported in MockStoreProxy.")
        };

        private Task<IUnitOfWorkTransaction> BeginTransaction()
        {
            BeginTransactionCalls++;
            return Task.FromResult<IUnitOfWorkTransaction>(new MockTransaction());
        }

        private object? Stage()
        {
            StageCalls++;
            return null;
        }

        private sealed class MockTransaction : IUnitOfWorkTransaction
        {
            public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }

    private sealed class TrackingUnitOfWork : IUnitOfWork
    {
        public int BeginTransactionCalls { get; private set; }
        public readonly AuthTestFixture InnerFixture = new();

        public IUserRepository UserRepository => InnerFixture;

        public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
        {
            BeginTransactionCalls++;
            return InnerFixture.BeginTransactionAsync(cancellationToken);
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            InnerFixture.SaveChangesAsync(cancellationToken);
    }

    private sealed class MockOtpSender : IOtpSender
    {
        public Task SendAsync(string channel, string destination, string code, CancellationToken ct) => Task.CompletedTask;
    }

    [Fact]
    public async Task DriverRegistration_WhenTokenMissing_ThrowsBotRequired400_BeforeTransactionOrStore()
    {
        var store = DispatchProxy.Create<IIdentityWorkflowStore, MockStoreProxy>();
        var proxy = (MockStoreProxy)(object)store;
        var verifier = new MockBotVerificationService();
        var service = new DriverRegistrationService(store, new BcryptPasswordService(), new MockOtpSender(), TimeProvider.System, verifier);

        var request = new DriverRegistrationDto
        {
            FullName = "Driver Test",
            Email = "driver@example.com",
            Password = "Password@123",
            TurnstileToken = null
        };

        var ex = await Assert.ThrowsAsync<AuthException>(() => service.RegisterAsync(request, CancellationToken.None));

        Assert.Equal("BOT_VERIFICATION_REQUIRED", ex.Code);
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(1, verifier.CallCount);
        Assert.Equal("driver_register", verifier.LastAction);
        Assert.Equal(0, proxy.BeginTransactionCalls);
        Assert.Equal(0, proxy.StageCalls);
    }

    [Fact]
    public async Task DriverRegistration_WhenTokenInvalid_ThrowsBotFailed400_BeforeTransactionOrStore()
    {
        var store = DispatchProxy.Create<IIdentityWorkflowStore, MockStoreProxy>();
        var proxy = (MockStoreProxy)(object)store;
        var verifier = new MockBotVerificationService();
        var service = new DriverRegistrationService(store, new BcryptPasswordService(), new MockOtpSender(), TimeProvider.System, verifier);

        var request = new DriverRegistrationDto
        {
            FullName = "Driver Test",
            Email = "driver@example.com",
            Password = "Password@123",
            TurnstileToken = "invalid-token"
        };

        var ex = await Assert.ThrowsAsync<AuthException>(() => service.RegisterAsync(request, CancellationToken.None));

        Assert.Equal("BOT_VERIFICATION_FAILED", ex.Code);
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(1, verifier.CallCount);
        Assert.Equal(0, proxy.BeginTransactionCalls);
        Assert.Equal(0, proxy.StageCalls);
    }

    [Fact]
    public async Task OwnerRegistration_WhenTokenMissing_ThrowsBotRequired400_BeforeTransactionOrStore()
    {
        var store = DispatchProxy.Create<IIdentityWorkflowStore, MockStoreProxy>();
        var proxy = (MockStoreProxy)(object)store;
        var verifier = new MockBotVerificationService();
        var passwords = new BcryptPasswordService();
        var authorization = DispatchProxy.Create<IAccountAuthorizationService, MockStoreProxy>();
        var workflows = new AccountWorkflowService(store, passwords, new TestWorkflowProtector(), TimeProvider.System, authorization);
        var service = new OwnerRegistrationService(store, passwords, TimeProvider.System, new TestParkingDirectory(), workflows, authorization, verifier);

        var request = new OwnerRegistrationDto
        {
            FullName = "Owner Test",
            BusinessName = "Biz",
            Email = "owner@example.com",
            Phone = "0901234567",
            LotType = "outdoor",
            AgreedToPolicy = true,
            TurnstileToken = null
        };

        var ex = await Assert.ThrowsAsync<AuthException>(() => service.RegisterAsync(request, CancellationToken.None));

        Assert.Equal("BOT_VERIFICATION_REQUIRED", ex.Code);
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(1, verifier.CallCount);
        Assert.Equal("owner_register", verifier.LastAction);
        Assert.Equal(0, proxy.BeginTransactionCalls);
        Assert.Equal(0, proxy.StageCalls);
    }

    [Fact]
    public async Task OwnerRegistration_WhenTokenInvalid_ThrowsBotFailed400_BeforeTransactionOrStore()
    {
        var store = DispatchProxy.Create<IIdentityWorkflowStore, MockStoreProxy>();
        var proxy = (MockStoreProxy)(object)store;
        var verifier = new MockBotVerificationService();
        var passwords = new BcryptPasswordService();
        var authorization = DispatchProxy.Create<IAccountAuthorizationService, MockStoreProxy>();
        var workflows = new AccountWorkflowService(store, passwords, new TestWorkflowProtector(), TimeProvider.System, authorization);
        var service = new OwnerRegistrationService(store, passwords, TimeProvider.System, new TestParkingDirectory(), workflows, authorization, verifier);

        var request = new OwnerRegistrationDto
        {
            FullName = "Owner Test",
            BusinessName = "Biz",
            Email = "owner@example.com",
            Phone = "0901234567",
            LotType = "outdoor",
            AgreedToPolicy = true,
            TurnstileToken = "invalid-token"
        };

        var ex = await Assert.ThrowsAsync<AuthException>(() => service.RegisterAsync(request, CancellationToken.None));

        Assert.Equal("BOT_VERIFICATION_FAILED", ex.Code);
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(1, verifier.CallCount);
        Assert.Equal(0, proxy.BeginTransactionCalls);
        Assert.Equal(0, proxy.StageCalls);
    }

    [Fact]
    public async Task LoginCommand_WhenTokenMissing_ThrowsBotRequired400_BeforeTransactionOrDatabase()
    {
        var uow = new TrackingUnitOfWork();
        var verifier = new MockBotVerificationService();
        var handler = new LoginCommandHandler(uow, new BcryptPasswordService(), uow.InnerFixture.Auth, uow.InnerFixture.Policy, uow.InnerFixture.Clock, null, verifier);

        var command = new LoginCommand
        {
            Email = "driver@gmail.com",
            Password = "Password@123",
            TurnstileToken = null
        };

        var ex = await Assert.ThrowsAsync<AuthException>(() => handler.Handle(command, CancellationToken.None));

        Assert.Equal("BOT_VERIFICATION_REQUIRED", ex.Code);
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(1, verifier.CallCount);
        Assert.Equal("password_login", verifier.LastAction);
        Assert.Equal(0, uow.BeginTransactionCalls);
    }

    [Fact]
    public async Task LoginCommand_WhenTokenInvalid_ThrowsBotFailed400_BeforeTransactionOrDatabase()
    {
        var uow = new TrackingUnitOfWork();
        var verifier = new MockBotVerificationService();
        var handler = new LoginCommandHandler(uow, new BcryptPasswordService(), uow.InnerFixture.Auth, uow.InnerFixture.Policy, uow.InnerFixture.Clock, null, verifier);

        var command = new LoginCommand
        {
            Email = "driver@gmail.com",
            Password = "Password@123",
            TurnstileToken = "invalid-token"
        };

        var ex = await Assert.ThrowsAsync<AuthException>(() => handler.Handle(command, CancellationToken.None));

        Assert.Equal("BOT_VERIFICATION_FAILED", ex.Code);
        Assert.Equal(400, ex.StatusCode);
        Assert.Equal(1, verifier.CallCount);
        Assert.Equal(0, uow.BeginTransactionCalls);
    }

    [Fact]
    public async Task LoginCommand_WhenTokenValid_CompletesBotVerificationAndExecutesLogin()
    {
        var uow = new TrackingUnitOfWork();
        var verifier = new MockBotVerificationService();
        var handler = new LoginCommandHandler(uow, new BcryptPasswordService(), uow.InnerFixture.Auth, uow.InnerFixture.Policy, uow.InnerFixture.Clock, null, verifier);

        var command = new LoginCommand
        {
            Email = "driver@gmail.com",
            Password = "Password@123",
            TurnstileToken = "valid-token-123"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, verifier.CallCount);
        Assert.Equal("valid-token-123", verifier.LastToken);
        Assert.Equal(1, uow.BeginTransactionCalls);
    }

    private sealed class MockDriverRegistration : IDriverRegistrationService
    {
        public Task<DriverRegistrationResult> RegisterAsync(DriverRegistrationDto body, CancellationToken ct) =>
            Task.FromResult(new DriverRegistrationResult(Guid.NewGuid(), body.Email ?? "email", DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow.AddMinutes(1)));
        public Task VerifyAsync(VerifyDriverOtpDto body, CancellationToken ct) => Task.CompletedTask;
        public Task<DriverRegistrationResult> ResendAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(new DriverRegistrationResult(id, "email", DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow.AddMinutes(1)));
        public Task<DriverRegistrationResult> RecoverAsync(string contact, CancellationToken ct) =>
            ResendAsync(Guid.NewGuid(), ct);
    }

    [Fact]
    public async Task HttpPipeline_Login_WhenBotTokenMissing_Returns400BotVerificationRequired_BypassingSignInErrors()
    {
        var verifier = new MockBotVerificationService();
        var fixture = new AuthTestFixture();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = false;
            options.ValidateScopes = false;
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddApiErrorResponses();
        builder.Services.AddApiCurrentActor();
        builder.Services.AddApplicationServices(new AuthenticationPolicy());
        builder.Services.AddSingleton<IVehicleRepository, UnsupportedVehicleRepository>();
        builder.Services.AddSingleton<IRegistrationClaimRepository, UnsupportedVehicleRepository>();
        builder.Services.AddSingleton<IIdentityAccessRepository, UnusedIdentityRepository>();
        builder.Services.AddSingleton<IParkingDirectory, TestParkingDirectory>();
        builder.Services.AddSingleton<IWorkflowProtector, TestWorkflowProtector>();
        TestAuthorization.AddServices(builder.Services, fixture);
        builder.Services.AddSingleton<IBotVerificationService>(verifier);
        builder.Services.AddSingleton<IUnitOfWork>(fixture);
        builder.Services.AddSingleton<ICurrentAccessStore>(fixture);
        builder.Services.AddSingleton<IEventPublisher, RecordingProfilePublisher>();
        builder.Services.AddSingleton<IAuthSessionStore>(fixture.Sessions);
        builder.Services.AddSingleton<IPasswordService, BcryptPasswordService>();
        builder.Services.AddSingleton<TimeProvider>(fixture.Clock);
        builder.Services.AddJWTAuthentication(builder.Configuration, true);
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(options =>
        {
            options.AddPolicy("LoginRateLimit", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("test"));
            options.AddPolicy("RegistrationRateLimit", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("test"));
        });

        await using var app = builder.Build();
        app.UseRouting();
        app.UseApiErrorResponses();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapControllers();
        await app.StartAsync();

        using var client = app.GetTestClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "driver@gmail.com", password = "Password@123" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(envelope.GetProperty("success").GetBoolean());
        Assert.Equal("BOT_VERIFICATION_REQUIRED", envelope.GetProperty("code").GetString());
        Assert.Equal("Bot verification token is required.", envelope.GetProperty("message").GetString());
    }

    [Fact]
    public async Task HttpPipeline_Login_WhenBotTokenValid_Returns200Success()
    {
        var verifier = new MockBotVerificationService();
        var fixture = new AuthTestFixture();

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = false;
            options.ValidateScopes = false;
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddApiErrorResponses();
        builder.Services.AddApiCurrentActor();
        builder.Services.AddApplicationServices(new AuthenticationPolicy());
        builder.Services.AddSingleton<IVehicleRepository, UnsupportedVehicleRepository>();
        builder.Services.AddSingleton<IRegistrationClaimRepository, UnsupportedVehicleRepository>();
        builder.Services.AddSingleton<IIdentityAccessRepository, UnusedIdentityRepository>();
        builder.Services.AddSingleton<IParkingDirectory, TestParkingDirectory>();
        builder.Services.AddSingleton<IWorkflowProtector, TestWorkflowProtector>();
        TestAuthorization.AddServices(builder.Services, fixture);
        builder.Services.AddSingleton<IBotVerificationService>(verifier);
        builder.Services.AddSingleton<IUnitOfWork>(fixture);
        builder.Services.AddSingleton<ICurrentAccessStore>(fixture);
        builder.Services.AddSingleton<IEventPublisher, RecordingProfilePublisher>();
        builder.Services.AddSingleton<IAuthSessionStore>(fixture.Sessions);
        builder.Services.AddSingleton<IPasswordService, BcryptPasswordService>();
        builder.Services.AddSingleton<TimeProvider>(fixture.Clock);
        builder.Services.AddJWTAuthentication(builder.Configuration, true);
        builder.Services.AddAuthorization();
        builder.Services.AddRateLimiter(options =>
        {
            options.AddPolicy("LoginRateLimit", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("test"));
            options.AddPolicy("RegistrationRateLimit", _ => System.Threading.RateLimiting.RateLimitPartition.GetNoLimiter("test"));
        });

        await using var app = builder.Build();
        app.UseRouting();
        app.UseApiErrorResponses();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapControllers();
        await app.StartAsync();

        using var client = app.GetTestClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "driver@gmail.com",
            password = "Password@123",
            turnstileToken = "valid-browser-token"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(envelope.GetProperty("success").GetBoolean());
        Assert.Equal("Login successful", envelope.GetProperty("message").GetString());
    }

    [Fact]
    public async Task HttpPipeline_RateLimiting_SeparatesRegistrationAndLoginLimits()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseDefaultServiceProvider(options =>
        {
            options.ValidateOnBuild = false;
            options.ValidateScopes = false;
        });
        builder.Logging.ClearProviders();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(AuthController).Assembly);
        builder.Services.AddApiErrorResponses();
        builder.Services.AddApiCurrentActor();
        builder.Services.AddApplicationServices(new AuthenticationPolicy());
        var fixture = new AuthTestFixture();
        builder.Services.AddSingleton<IVehicleRepository, UnsupportedVehicleRepository>();
        builder.Services.AddSingleton<IRegistrationClaimRepository, UnsupportedVehicleRepository>();
        builder.Services.AddSingleton<IIdentityAccessRepository, UnusedIdentityRepository>();
        builder.Services.AddSingleton<IParkingDirectory, TestParkingDirectory>();
        builder.Services.AddSingleton<IWorkflowProtector, TestWorkflowProtector>();
        TestAuthorization.AddServices(builder.Services, fixture);
        builder.Services.AddSingleton<IDriverRegistrationService, MockDriverRegistration>();
        builder.Services.AddSingleton<IUnitOfWork>(fixture);
        builder.Services.AddSingleton<ICurrentAccessStore>(fixture);
        builder.Services.AddSingleton<IEventPublisher, RecordingProfilePublisher>();
        builder.Services.AddSingleton<IAuthSessionStore>(fixture.Sessions);
        builder.Services.AddSingleton<IPasswordService, BcryptPasswordService>();
        builder.Services.AddSingleton<TimeProvider>(fixture.Clock);
        builder.Services.AddSingleton<IBotVerificationService>(new MockBotVerificationService());
        builder.Services.AddJWTAuthentication(builder.Configuration, true);
        builder.Services.AddAuthorization();

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("RegistrationRateLimit", context =>
                System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new()
                    { PermitLimit = 2, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            options.AddPolicy("LoginRateLimit", context =>
                System.Threading.RateLimiting.RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new()
                    { PermitLimit = 2, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        await using var app = builder.Build();
        app.UseForwardedHeaders();
        app.UseRouting();
        app.UseApiErrorResponses();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapControllers();
        await app.StartAsync();

        using var client = app.GetTestClient();

        // 2 registration requests succeed / validate
        var reg1 = await client.PostAsJsonAsync("/api/auth/register/driver", new { fullName = "D1", email = "d1@test.com", password = "Password@123", turnstileToken = "token" });
        var reg2 = await client.PostAsJsonAsync("/api/auth/register/driver", new { fullName = "D2", email = "d2@test.com", password = "Password@123", turnstileToken = "token" });
        Assert.NotEqual(HttpStatusCode.TooManyRequests, reg1.StatusCode);
        Assert.NotEqual(HttpStatusCode.TooManyRequests, reg2.StatusCode);

        // 3rd registration request is rate-limited (429)
        var reg3 = await client.PostAsJsonAsync("/api/auth/register/driver", new { fullName = "D3", email = "d3@test.com", password = "Password@123", turnstileToken = "token" });
        Assert.Equal(HttpStatusCode.TooManyRequests, reg3.StatusCode);

        // Login quota is separate: login request still passes through without 429
        var login1 = await client.PostAsJsonAsync("/api/auth/login", new { email = "driver@gmail.com", password = "Password@123", turnstileToken = "token" });
        Assert.Equal(HttpStatusCode.OK, login1.StatusCode);
    }
}
