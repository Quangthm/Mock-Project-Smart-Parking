using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SmartParking.PaymentService.API.Middleware;
using SmartParking.PaymentService.Application.Usecase.ProcessWebhook;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.Tests.Integration;

public sealed class PaymentExceptionHandlerTests
{
    [Theory]
    [InlineData("QUOTE_NOT_FOUND", 404)]
    [InlineData("QUOTE_BINDING_CONFLICT", 409)]
    [InlineData("GATEWAY_TIMEOUT", 504)]
    [InlineData("unexpected", 500)]
    public async Task HandlerWritesOnlyCanonicalSafeError(string code, int status)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var body = new MemoryStream();
        var context = new DefaultHttpContext { TraceIdentifier = "trace-1", RequestServices = services };
        context.Response.Body = body;
        var handler = new PaymentExceptionHandler(NullLogger<PaymentExceptionHandler>.Instance);
        const string detail = "private diagnostic details";
        Exception exception = status == 500 ? new InvalidOperationException(detail) : new PaymentDomainException(code, detail);

        Assert.True(await handler.TryHandleAsync(context, exception, default));

        Assert.Equal(status, context.Response.StatusCode);
        body.Position = 0;
        using var json = await JsonDocument.ParseAsync(body);
        var error = json.RootElement;
        Assert.False(error.GetProperty("success").GetBoolean());
        Assert.Equal(status == 500 ? "INTERNAL_SERVER_ERROR" : code, error.GetProperty("code").GetString());
        Assert.False(error.TryGetProperty("errorCode", out _));
        Assert.False(error.TryGetProperty("status", out _));
        Assert.NotEmpty(error.GetProperty("traceId").GetString()!);
        Assert.DoesNotContain(detail, error.GetRawText());
    }

    [Fact]
    public async Task UnexpectedCallbackFailureKeepsProviderErrorAcknowledgement()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Response.Body = body;
        context.Items[typeof(ProcessPayOsWebhookCommand)] = System.Diagnostics.Stopwatch.GetTimestamp();
        var handler = new PaymentExceptionHandler(NullLogger<PaymentExceptionHandler>.Instance);

        Assert.True(await handler.TryHandleAsync(context, new IOException("private provider details"), default));
        Assert.Equal(500, context.Response.StatusCode);
        body.Position = 0;
        using var json = await JsonDocument.ParseAsync(body);
        Assert.False(json.RootElement.GetProperty("success").GetBoolean());
        Assert.Equal("INTERNAL_SERVER_ERROR", json.RootElement.GetProperty("errorCode").GetString());
        Assert.False(json.RootElement.TryGetProperty("code", out _));
        Assert.DoesNotContain("private provider details", json.RootElement.GetRawText());
    }
}
