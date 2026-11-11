using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.FileProviders;
using SmartParking.NotificationAuditService.API.Services;

namespace SmartParking.NotificationAuditService.Tests.Security;

public class CurrentUserServiceSecurityTests
{
    private class FakeWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "SmartParking.NotificationAuditService";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    [Fact]
    public void NonDevelopment_WhenXUserIdHeaderProvidedWithoutClaims_ShouldRejectHeaderSpoofing()
    {
        // Arrange
        var env = new FakeWebHostEnvironment { EnvironmentName = "Production" };
        var httpContext = new DefaultHttpContext();
        var spoofedUserId = Guid.NewGuid();
        httpContext.Request.Headers["X-User-Id"] = spoofedUserId.ToString();
        httpContext.Request.Headers["X-User-Role"] = "admin";

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var currentUserService = new CurrentUserService(accessor, env);

        // Act & Assert
        Assert.Null(currentUserService.UserId);
        Assert.Null(currentUserService.UserRole);
        Assert.False(currentUserService.IsAuthenticated);
    }

    [Fact]
    public void NonDevelopment_WhenStagingEnvironment_ShouldRejectHeaderSpoofing()
    {
        // Arrange
        var env = new FakeWebHostEnvironment { EnvironmentName = "Staging" };
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = Guid.NewGuid().ToString();
        httpContext.Request.Headers["X-User-Role"] = "operator";

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var currentUserService = new CurrentUserService(accessor, env);

        // Act & Assert
        Assert.Null(currentUserService.UserId);
        Assert.Null(currentUserService.UserRole);
        Assert.False(currentUserService.IsAuthenticated);
    }

    [Fact]
    public void Development_WhenXUserIdHeaderProvidedWithoutClaims_ShouldAllowFallback()
    {
        // Arrange
        var env = new FakeWebHostEnvironment { EnvironmentName = "Development" };
        var httpContext = new DefaultHttpContext();
        var expectedUserId = Guid.NewGuid();
        httpContext.Request.Headers["X-User-Id"] = expectedUserId.ToString();
        httpContext.Request.Headers["X-User-Role"] = "driver";

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var currentUserService = new CurrentUserService(accessor, env);

        // Act & Assert
        Assert.Equal(expectedUserId, currentUserService.UserId);
        Assert.Equal("driver", currentUserService.UserRole);
        Assert.True(currentUserService.IsAuthenticated);
    }

    [Fact]
    public void Development_WhenMalformedGuidHeaderProvided_ShouldReturnNull()
    {
        // Arrange
        var env = new FakeWebHostEnvironment { EnvironmentName = "Development" };
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers["X-User-Id"] = "not-a-valid-guid";
        httpContext.Request.Headers["X-User-Role"] = "";

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var currentUserService = new CurrentUserService(accessor, env);

        // Act & Assert
        Assert.Null(currentUserService.UserId);
        Assert.Null(currentUserService.UserRole);
        Assert.False(currentUserService.IsAuthenticated);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    [InlineData("Staging")]
    public void AuthenticatedUser_WithSubClaim_ShouldExtractUserIdSuccessfullyInAnyEnvironment(string environmentName)
    {
        // Arrange
        var env = new FakeWebHostEnvironment { EnvironmentName = environmentName };
        var expectedUserId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim("sub", expectedUserId.ToString()),
            new Claim("role", "driver")
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var currentUserService = new CurrentUserService(accessor, env);

        // Act & Assert
        Assert.Equal(expectedUserId, currentUserService.UserId);
        Assert.Equal("driver", currentUserService.UserRole);
        Assert.True(currentUserService.IsAuthenticated);
    }

    [Fact]
    public void AuthenticatedUser_WithNameIdentifierAndRoleClaims_ShouldExtractCorrectly()
    {
        // Arrange
        var env = new FakeWebHostEnvironment { EnvironmentName = "Production" };
        var expectedUserId = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, expectedUserId.ToString()),
            new Claim(ClaimTypes.Role, "admin")
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var currentUserService = new CurrentUserService(accessor, env);

        // Act & Assert
        Assert.Equal(expectedUserId, currentUserService.UserId);
        Assert.Equal("admin", currentUserService.UserRole);
        Assert.True(currentUserService.IsAuthenticated);
    }

    [Fact]
    public void AuthenticatedUser_ClaimsShouldTakePrecedenceOverSpoofedHeaders()
    {
        // Arrange
        var env = new FakeWebHostEnvironment { EnvironmentName = "Development" };
        var legitimateUserId = Guid.NewGuid();
        var attackerSpoofedUserId = Guid.NewGuid();

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, legitimateUserId.ToString()),
            new Claim(ClaimTypes.Role, "driver")
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.Request.Headers["X-User-Id"] = attackerSpoofedUserId.ToString();
        httpContext.Request.Headers["X-User-Role"] = "admin";

        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var currentUserService = new CurrentUserService(accessor, env);

        // Act & Assert: Claims MUST take priority over headers
        Assert.Equal(legitimateUserId, currentUserService.UserId);
        Assert.Equal("driver", currentUserService.UserRole);
        Assert.True(currentUserService.IsAuthenticated);
    }

    [Fact]
    public void Unauthenticated_WithNoHttpContext_ReturnsNullAndFalse()
    {
        // Arrange
        var env = new FakeWebHostEnvironment { EnvironmentName = "Production" };
        var accessor = new HttpContextAccessor { HttpContext = null };
        var currentUserService = new CurrentUserService(accessor, env);

        // Act & Assert
        Assert.Null(currentUserService.UserId);
        Assert.Null(currentUserService.UserRole);
        Assert.False(currentUserService.IsAuthenticated);
    }
}
