using System.Net;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.PaymentService.API;
using SmartParking.PaymentService.Application.Integrations;
using Xunit;

namespace SmartParking.PaymentService.Tests;

/// <summary>
/// Response-compatibility coverage for the framework <see cref="IExceptionHandler"/>
/// replacement of the retired custom catch-all middleware. The envelope
/// <c>{ success, errorCode, message, traceId, errors, timestamp }</c> and the HTTP
/// status mapping must remain unchanged for existing API consumers.
/// </summary>
public class PaymentExceptionHandlerTests
{
    private const string AdminPaymentPath = "/api/v1/admin/integrations/payment";

    [Fact]
    public async Task IntegrationStorageFailure_ReturnsRetryable503WithoutProviderDetails()
    {
        var (status, body) = await HandleAsync(new PaymentIntegrationPersistenceException(
            "Private storage detail", new InvalidOperationException("synthetic-sensitive-detail")), AdminPaymentPath);
        Assert.Equal(503, status);
        AssertUniformEnvelope(body, PaymentIntegrationPersistenceException.ErrorCode);
        Assert.DoesNotContain("Private storage detail", body);
        Assert.DoesNotContain("synthetic-sensitive-detail", body);
    }

    private static async Task<(int StatusCode, string Body)> HandleAsync(
        Exception exception,
        string path = "/api/v1/payments")
    {
        var handler = new PaymentExceptionHandler(NullLogger<PaymentExceptionHandler>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);

        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return (context.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private static void AssertUniformEnvelope(string body, string expectedErrorCode)
    {
        Assert.Contains("\"success\":false", body);
        Assert.Contains($"\"code\":\"{expectedErrorCode}\"", body);
        Assert.Contains($"\"errorCode\":\"{expectedErrorCode}\"", body);
        Assert.Contains("\"message\":", body);
        Assert.Contains("\"traceId\":", body);
        Assert.Contains("\"errors\":", body);
        Assert.Contains("\"timestamp\":", body);
    }

    [Theory]
    [InlineData("INVALID_PAYMENT_BASE_URL")]
    [InlineData("INVALID_CREDENTIALS")]
    [InlineData("VALIDATION_FAILED")]
    [InlineData("SOMETHING_ELSE")]
    public async Task PaymentIntegrationException_MapsToBadRequest(string code)
    {
        var exception = new PaymentIntegrationException(code, "integration rejected");

        var (status, body) = await HandleAsync(exception, AdminPaymentPath);

        Assert.Equal((int)HttpStatusCode.BadRequest, status);
        AssertUniformEnvelope(body, code);
    }

    [Fact]
    public async Task PaymentIntegrationException_PreservesFieldErrors()
    {
        var exception = new PaymentIntegrationException(
            "VALIDATION_FAILED",
            "integration rejected",
            new[] { new ApiFieldError("Mode", "Mode is required.") });

        var (status, body) = await HandleAsync(exception, AdminPaymentPath);

        Assert.Equal((int)HttpStatusCode.BadRequest, status);
        AssertUniformEnvelope(body, "VALIDATION_FAILED");
        Assert.Contains("\"property\":\"Mode\"", body);
        Assert.Contains("\"message\":\"Mode is required.\"", body);
    }

    [Theory]
    [InlineData(AdminPaymentPath, "VALIDATION_FAILED")]
    [InlineData("/api/v1/payments", "VALIDATION_ERROR")]
    public async Task ValidationException_UsesPathDependentErrorCode(string path, string expectedCode)
    {
        var exception = new ValidationException(new[]
        {
            new ValidationFailure("Amount", "Amount must be positive.") { ErrorCode = "GreaterThan" }
        });

        var (status, body) = await HandleAsync(exception, path);

        Assert.Equal((int)HttpStatusCode.BadRequest, status);
        AssertUniformEnvelope(body, expectedCode);
        Assert.Contains("\"property\":\"Amount\"", body);
        Assert.Contains("\"code\":\"GreaterThan\"", body);
    }

    [Fact]
    public async Task BadHttpRequestException_MapsToBadRequest()
    {
        var exception = new BadHttpRequestException("malformed payload");

        var (status, body) = await HandleAsync(exception);

        Assert.Equal((int)HttpStatusCode.BadRequest, status);
        AssertUniformEnvelope(body, "BAD_REQUEST");
    }

    [Fact]
    public async Task UnhandledException_FallsBackToInternalServerError()
    {
        var exception = new InvalidOperationException("boom");

        var (status, body) = await HandleAsync(exception);

        Assert.Equal((int)HttpStatusCode.InternalServerError, status);
        AssertUniformEnvelope(body, "INTERNAL_SERVER_ERROR");
        // The raw exception message must never leak in the response envelope.
        Assert.DoesNotContain("boom", body);
    }
}
