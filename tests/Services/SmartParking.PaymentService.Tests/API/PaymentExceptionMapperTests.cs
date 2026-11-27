using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmartParking.PaymentService.API.Common;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Tests.API;

public sealed class PaymentExceptionMapperTests
{
    [Fact]
    public void RegistryRejectsDuplicateAndNonExceptionTypesAndUsesExactMatching()
    {
        var mapping = new KeyValuePair<Type, Func<Exception, PaymentErrorHandlingResult>>(
            typeof(ArgumentException),
            _ => new(PaymentErrorResponse.Error(400, "EXAMPLE", "Example")));

        Assert.Throws<ArgumentException>(() => new PaymentExceptionMapper([mapping, mapping]));
        Assert.Throws<ArgumentException>(() => new PaymentExceptionMapper([
            new KeyValuePair<Type, Func<Exception, PaymentErrorHandlingResult>>(
                typeof(string),
                _ => new(PaymentErrorResponse.Error(400, "INVALID", "Invalid")))
        ]));

        var mapper = new PaymentExceptionMapper([mapping]);
        Assert.Equal(400, mapper.Map(new ArgumentException()).Response.StatusCode);
        Assert.True(mapper.Map(new ArgumentNullException("secret")).IsUnexpected);
    }

    [Theory]
    [InlineData(PaymentDomainException.InvoiceNotFound, 404)]
    [InlineData(PaymentDomainException.OrderNotFound, 404)]
    [InlineData(PaymentDomainException.TariffNotFound, 404)]
    [InlineData("TRANSACTION_NOT_FOUND", 404)]
    [InlineData("QUOTE_NOT_FOUND", 404)]
    [InlineData("COLLECTION_NOT_FOUND", 404)]
    [InlineData(PaymentDomainException.Unauthorized, 401)]
    [InlineData(PaymentDomainException.ForbiddenInvoiceAccess, 403)]
    [InlineData(PaymentDomainException.OperatorSiteMismatch, 403)]
    [InlineData(PaymentDomainException.CrossTenantAccessDenied, 403)]
    [InlineData("FORBIDDEN_ORDER_ACCESS", 403)]
    [InlineData("FORBIDDEN_RESERVATION_DEPOSIT_CREATION", 403)]
    [InlineData(PaymentDomainException.OrderAlreadyPaid, 409)]
    [InlineData("ORDER_ALREADY_PAID_ONLINE", 409)]
    [InlineData("ORDER_INVALID_STATUS", 409)]
    [InlineData(PaymentDomainException.IdempotencyPayloadMismatch, 409)]
    [InlineData("IDEMPOTENCY_CONFLICT", 409)]
    [InlineData("PAYMENT_RETRY_LIMIT_EXCEEDED", 409)]
    [InlineData("PAYMENT_TRANSACTION_AMBIGUOUS", 409)]
    [InlineData("UNKNOWN_TRANSACTION_EXISTS", 409)]
    [InlineData("PAYOS_CHECKOUT_LINK_UNAVAILABLE", 409)]
    [InlineData("TERMS_STALE", 409)]
    [InlineData("QUOTE_EXPIRED", 409)]
    [InlineData("QUOTE_STATUS_INVALID", 409)]
    [InlineData("TRANSACTION_NOT_SETTLED", 409)]
    [InlineData(PaymentDomainException.UnsettledPaymentOrder, 422)]
    [InlineData(PaymentDomainException.OrderExpired, 422)]
    [InlineData("ZERO_AMOUNT_ORDER", 422)]
    [InlineData("INVALID_ORDER_CODE", 422)]
    [InlineData(PaymentDomainException.InvalidCutoffTime, 400)]
    [InlineData("INVALID_WEBHOOK_SIGNATURE", 400)]
    [InlineData("QUOTE_ACCOUNT_MISMATCH", 400)]
    [InlineData("QUOTE_AMOUNT_MISMATCH", 400)]
    [InlineData("QUOTE_TENANT_MISMATCH", 400)]
    [InlineData("QUOTE_SITE_MISMATCH", 400)]
    [InlineData("QUOTE_CURRENCY_MISMATCH", 400)]
    [InlineData("INVALID_AMOUNT", 400)]
    [InlineData("INVALID_TEST_AMOUNT", 400)]
    [InlineData("UNSUPPORTED_VEHICLE_TYPE", 400)]
    public void PaymentDomainExceptionsMapToExpectedHttpStatus(string code, int expectedStatus)
    {
        var mapper = PaymentExceptionMapper.CreateDefault();
        var result = mapper.Map(new PaymentDomainException(code, "Domain rejection", "orderId"));

        Assert.False(result.IsUnexpected);
        Assert.Equal(expectedStatus, result.Response.StatusCode);
        Assert.Equal(code, result.Response.Code);
        Assert.Single(result.Response.Errors!);
    }

    [Theory]
    [InlineData("GATEWAY_TIMEOUT", 504)]
    [InlineData("PAYOS_CREDENTIALS_NOT_CONFIGURED", 503)]
    [InlineData("PAYOS_CHECKOUT_CANCELLATION_FAILED", 502)]
    [InlineData("GATEWAY_INITIATION_FAILED", 502)]
    [InlineData("PAYOS_500", 502)]
    [InlineData("PAYOS_400", 502)]
    public void ProviderFailuresAreSanitizedWithoutExposingSecrets(string code, int expectedStatus)
    {
        var mapper = PaymentExceptionMapper.CreateDefault();
        var result = mapper.Map(new PaymentDomainException(
            code,
            "secret-client-id=live-key-123 raw PayOS internal payload",
            "secretField"));

        Assert.False(result.IsUnexpected);
        Assert.Equal(expectedStatus, result.Response.StatusCode);
        Assert.Equal(code, result.Response.Code);
        Assert.DoesNotContain("secret", result.Response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("live-key", result.Response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.Response.Errors);
    }

    [Theory]
    [InlineData("TARIFF_NOT_FOUND", 404)]
    [InlineData("CORRUPTED_POLICY_SNAPSHOT", 422)]
    [InlineData("INVALID_TIME_RANGE", 422)]
    [InlineData("PARKING_SERVICE_UNAVAILABLE", 503)]
    [InlineData("RESERVATION_SERVICE_UNAVAILABLE", 503)]
    [InlineData("PARKING_SERVICE_TIMEOUT", 504)]
    [InlineData("RESERVATION_SERVICE_TIMEOUT", 504)]
    [InlineData("UNSUPPORTED_VEHICLE_TYPE", 400)]
    [InlineData("VEHICLE_TYPE_MISMATCH", 422)]
    [InlineData("INVALID_PRICING_POLICY", 422)]
    [InlineData("INVALID_TIMEZONE", 422)]
    [InlineData("OVERLAPPING_TIME_BLOCKS", 422)]
    [InlineData("UNCOVERED_TIME_BLOCKS", 422)]
    public void PricingDomainExceptionsMapToExpectedHttpStatus(string code, int expectedStatus)
    {
        var mapper = PaymentExceptionMapper.CreateDefault();
        var result = mapper.Map(new PricingDomainException(code, "Pricing error", "vehicleType"));

        Assert.False(result.IsUnexpected);
        Assert.Equal(expectedStatus, result.Response.StatusCode);
        Assert.Equal(code, result.Response.Code);
        Assert.Single(result.Response.Errors!);
    }

    [Fact]
    public void ValidationConcurrencyConflictAndBadHttpRequestMapSafely()
    {
        var mapper = PaymentExceptionMapper.CreateDefault();

        var validation = mapper.Map(new ValidationException([
            new ValidationFailure("amount", "Amount must be positive.") { ErrorCode = "INVALID_AMOUNT" }
        ]));
        Assert.False(validation.IsUnexpected);
        Assert.Equal(400, validation.Response.StatusCode);
        Assert.Equal("VALIDATION_ERROR", validation.Response.Code);
        Assert.Equal("INVALID_AMOUNT", Assert.Single(validation.Response.Errors!).Code);

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
        Assert.DoesNotContain("secret", directPg.Response.Message, StringComparison.OrdinalIgnoreCase);

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
    }

    [Fact]
    public void UnregisteredAndGenericExceptionsFallBackToSafe500()
    {
        var mapper = PaymentExceptionMapper.CreateDefault();

        foreach (var ex in new Exception[]
        {
            new KeyNotFoundException("secret missing entity"),
            new UnauthorizedAccessException("secret filesystem path"),
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
