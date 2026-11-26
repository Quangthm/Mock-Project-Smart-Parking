using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.NotificationAuditService.API.Common;
using SmartParking.NotificationAuditService.Domain.Exceptions;

namespace SmartParking.NotificationAuditService.Tests.API;

public sealed class NotificationAuditExceptionMapperTests
{
    [Fact]
    public void RegistryRejectsDuplicateAndNonExceptionTypesAndUsesExactMatching()
    {
        var mapping = new KeyValuePair<Type, Func<Exception, NotificationErrorHandlingResult>>(
            typeof(ArgumentException),
            _ => new(NotificationErrorResponse.Error(400, "EXAMPLE", "Example")));

        Assert.Throws<ArgumentException>(() => new NotificationAuditExceptionMapper([mapping, mapping]));
        Assert.Throws<ArgumentException>(() => new NotificationAuditExceptionMapper([
            new KeyValuePair<Type, Func<Exception, NotificationErrorHandlingResult>>(
                typeof(string),
                _ => new(NotificationErrorResponse.Error(400, "INVALID", "Invalid")))
        ]));

        var mapper = new NotificationAuditExceptionMapper([mapping]);
        Assert.Equal(400, mapper.Map(new ArgumentException()).Response.StatusCode);
        Assert.True(mapper.Map(new ArgumentNullException("secret")).IsUnexpected);
    }

    [Fact]
    public void KnownNotificationAuditExceptionsMapConsistentlyWithoutLeakingSecrets()
    {
        var mapper = NotificationAuditExceptionMapper.CreateDefault();

        var validation = mapper.Map(new ValidationException([
            new ValidationFailure("limit", "Limit must be positive.") { ErrorCode = "INVALID_LIMIT" }
        ]));
        Assert.False(validation.IsUnexpected);
        Assert.Equal(400, validation.Response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", validation.Response.Code);
        Assert.Equal("INVALID_LIMIT", Assert.Single(validation.Response.Errors!).Code);

        var domain = mapper.Map(new NotificationDomainException("Cannot retry an already delivered notification."));
        Assert.False(domain.IsUnexpected);
        Assert.Equal(400, domain.Response.StatusCode);
        Assert.Equal("DOMAIN_ERROR", domain.Response.Code);

        var forbidden = mapper.Map(new NotificationForbiddenException("secret actor=123 supportCase=confidential"));
        Assert.False(forbidden.IsUnexpected);
        Assert.Equal(403, forbidden.Response.StatusCode);
        Assert.Equal("FORBIDDEN", forbidden.Response.Code);
        Assert.DoesNotContain("secret", forbidden.Response.Message, StringComparison.OrdinalIgnoreCase);

        var notFound = mapper.Map(new KeyNotFoundException("secret audit record id=999"));
        Assert.False(notFound.IsUnexpected);
        Assert.Equal(404, notFound.Response.StatusCode);
        Assert.Equal("NOT_FOUND", notFound.Response.Code);
        Assert.DoesNotContain("secret", notFound.Response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ConcurrencyConflictAndBadHttpRequestMapSafely()
    {
        var mapper = NotificationAuditExceptionMapper.CreateDefault();

        var concurrency = mapper.Map(new DbUpdateConcurrencyException("secret row version"));
        Assert.False(concurrency.IsUnexpected);
        Assert.Equal(409, concurrency.Response.StatusCode);
        Assert.Equal("RESOURCE_VERSION_CONFLICT", concurrency.Response.Code);
        Assert.DoesNotContain("secret", concurrency.Response.Message, StringComparison.OrdinalIgnoreCase);

        var uniqueViolation = new PostgresException("duplicate key secret", "ERROR", "ERROR", PostgresErrorCodes.UniqueViolation);
        var directPg = mapper.Map(uniqueViolation);
        Assert.False(directPg.IsUnexpected);
        Assert.Equal(409, directPg.Response.StatusCode);
        Assert.Equal("RESOURCE_CONFLICT", directPg.Response.Code);

        var exclusionViolation = new PostgresException("exclusion conflict secret", "ERROR", "ERROR", PostgresErrorCodes.ExclusionViolation);
        var wrappedPg = mapper.Map(new DbUpdateException("secret save error", exclusionViolation));
        Assert.False(wrappedPg.IsUnexpected);
        Assert.Equal(409, wrappedPg.Response.StatusCode);
        Assert.Equal("RESOURCE_CONFLICT", wrappedPg.Response.Code);

        var badHttp = mapper.Map(new BadHttpRequestException("secret internal parser type", 415));
        Assert.False(badHttp.IsUnexpected);
        Assert.Equal(415, badHttp.Response.StatusCode);
        Assert.Equal("UNSUPPORTED_MEDIA_TYPE", badHttp.Response.Code);
        Assert.DoesNotContain("secret", badHttp.Response.Message, StringComparison.OrdinalIgnoreCase);

        var other4xx = mapper.Map(new BadHttpRequestException("secret header too large", 431));
        Assert.False(other4xx.IsUnexpected);
        Assert.Equal(431, other4xx.Response.StatusCode);
        Assert.Equal("INVALID_REQUEST", other4xx.Response.Code);
        Assert.DoesNotContain("secret", other4xx.Response.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnregisteredAndGenericExceptionsFallBackToSafe500()
    {
        var mapper = NotificationAuditExceptionMapper.CreateDefault();

        foreach (var ex in new Exception[]
        {
            new UnauthorizedAccessException("secret delivery credential path"),
            new InvalidOperationException("secret connection string Password=123")
        })
        {
            var result = mapper.Map(ex);
            Assert.True(result.IsUnexpected);
            Assert.Equal(500, result.Response.StatusCode);
            Assert.Equal("INTERNAL_SERVER_ERROR", result.Response.Code);
            Assert.DoesNotContain("secret", result.Response.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
