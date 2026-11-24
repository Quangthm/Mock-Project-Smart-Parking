using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using SmartParking.IoTService.API.Http;
using SmartParking.IoTService.Application.Assistant.Knowledge;

namespace SmartParking.IoTService.Tests;

public sealed class AssistantErrorTests
{
    [Theory]
    [InlineData(401, "INVALID_TOKEN")]
    [InlineData(403, "ASSISTANT_FORBIDDEN")]
    [InlineData(405, "METHOD_NOT_ALLOWED")]
    [InlineData(413, "PAYLOAD_TOO_LARGE")]
    [InlineData(415, "UNSUPPORTED_MEDIA_TYPE")]
    [InlineData(429, "RATE_LIMITED")]
    public async Task FormatterPreservesStatusAndHeaders(int status, string code)
    {
        var context = Context();
        context.Response.Headers.Allow = "GET";
        context.Response.Headers.RetryAfter = "2";
        await AssistantErrors.WriteAsync(context, AssistantErrors.ForStatus(status));
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal("GET", context.Response.Headers.Allow.ToString());
        Assert.Equal("2", context.Response.Headers.RetryAfter.ToString());
        if (status == 401) Assert.Equal("Bearer", context.Response.Headers.WWWAuthenticate.ToString());
        context.Response.Body.Position = 0;
        var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        Assert.False(body.RootElement.TryGetProperty("status", out _));
        Assert.Empty(body.RootElement.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public async Task UnknownExceptionIsSanitizedAndHeadIsBodyless()
    {
        var context = Context();
        var handler = new AssistantExceptionHandler(NullLogger<AssistantExceptionHandler>.Instance);
        Assert.True(await handler.TryHandleAsync(context, new InvalidOperationException("sensitive-value"), default));
        context.Response.Body.Position = 0;
        var text = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.DoesNotContain("sensitive-value", text);
        Assert.Equal(500, context.Response.StatusCode);
        var head = Context();
        head.Request.Method = "HEAD";
        await AssistantErrors.WriteAsync(head, AssistantErrors.ForStatus(404));
        Assert.Equal(0, head.Response.Body.Length);
    }

    [Fact]
    public async Task ClientCancellationIsNotRewritten()
    {
        var context = Context();
        context.RequestAborted = new CancellationToken(true);
        var handler = new AssistantExceptionHandler(NullLogger<AssistantExceptionHandler>.Instance);
        Assert.False(await handler.TryHandleAsync(context, new OperationCanceledException(), default));
        Assert.Equal(0, context.Response.Body.Length);
    }

    [Fact]
    public async Task ModelDiagnosticsStayInLogsAndDoNotChangeThePublicError()
    {
        var context = Context();
        var logger = new CaptureLogger();
        var handler = new AssistantExceptionHandler(logger);
        await handler.TryHandleAsync(context,
            new DemoGroundedUnavailableException(DemoGroundedFailure.ProviderRejected, 429), default);
        Assert.Equal(503, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        var body = await new StreamReader(context.Response.Body).ReadToEndAsync();
        Assert.Contains("MODEL_UNAVAILABLE", body);
        Assert.DoesNotContain("ProviderRejected", body);
        using var json = JsonDocument.Parse(body);
        Assert.False(json.RootElement.TryGetProperty("providerStatus", out _));
        Assert.False(json.RootElement.TryGetProperty("failure", out _));
        var log = Assert.Single(logger.Messages);
        Assert.Contains("ProviderRejected", log);
        Assert.Contains("429", log);
        Assert.Contains(context.TraceIdentifier, log);
    }

    [Theory]
    [InlineData(DemoGroundedFailure.Knowledge, null, "RETRIEVAL_UNAVAILABLE", "knowledge")]
    [InlineData(DemoGroundedFailure.Configuration, null, "ASSISTANT_NOT_CONFIGURED", "configured")]
    [InlineData(DemoGroundedFailure.Timeout, null, "MODEL_TIMEOUT", "in time")]
    [InlineData(DemoGroundedFailure.InvalidResponse, null, "INVALID_MODEL_OUTPUT", "unusable")]
    [InlineData(DemoGroundedFailure.ResponseTooLarge, null, "INVALID_MODEL_OUTPUT", "unusable")]
    [InlineData(DemoGroundedFailure.Busy, null, "MODEL_UNAVAILABLE", "busy")]
    [InlineData(DemoGroundedFailure.Network, null, "MODEL_UNAVAILABLE", "reached")]
    [InlineData(DemoGroundedFailure.ProviderRejected, 429, "MODEL_UNAVAILABLE", "rate limited")]
    [InlineData(DemoGroundedFailure.ProviderRejected, 401, "MODEL_UNAVAILABLE", "configuration")]
    [InlineData(DemoGroundedFailure.ProviderRejected, 403, "MODEL_UNAVAILABLE", "configuration")]
    [InlineData(DemoGroundedFailure.ProviderRejected, 404, "MODEL_UNAVAILABLE", "configuration")]
    [InlineData(DemoGroundedFailure.ProviderRejected, 503, "MODEL_UNAVAILABLE", "temporarily")]
    [InlineData(DemoGroundedFailure.Unspecified, null, "MODEL_UNAVAILABLE", "unavailable")]
    public async Task KnownFailureCategoryIsUsefulButContainsNoInternalDetails(DemoGroundedFailure failure, int? status, string code, string messagePart)
    {
        var context = Context();
        var handler = new AssistantExceptionHandler(NullLogger<AssistantExceptionHandler>.Instance);
        Assert.True(await handler.TryHandleAsync(context, new DemoGroundedUnavailableException(failure, status), default));
        Assert.Equal(503, context.Response.StatusCode);
        Assert.False(context.Response.Headers.ContainsKey("WWW-Authenticate"));
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        Assert.Contains(messagePart, body.RootElement.GetProperty("message").GetString());
        Assert.Empty(body.RootElement.GetProperty("errors").EnumerateArray());
        Assert.False(body.RootElement.TryGetProperty("providerStatus", out _));
        Assert.False(body.RootElement.TryGetProperty("failure", out _));
        Assert.False(body.RootElement.TryGetProperty("status", out _));
    }

    private sealed class CaptureLogger : ILogger<AssistantExceptionHandler>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Assert.Null(exception);
            Messages.Add(formatter(state, exception));
        }
    }

    private static DefaultHttpContext Context()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        return context;
    }
}
