using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.API.Middleware;
using SmartParking.PaymentService.Tests.API.Probes;

namespace SmartParking.PaymentService.Tests.API;

public sealed class PaymentApiErrorHandlingTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task ErrorCorrelationIsServerGeneratedInEveryEnvironment(string environment)
    {
        await using var host = await PaymentErrorTestHost.StartAsync(environment: environment);
        host.Client.DefaultRequestHeaders.Add("traceparent", "00-11111111111111111111111111111111-2222222222222222-01");

        foreach (var route in new[] { "/missing", "/error-probe/secure", "/error-probe/unexpected/value" })
        {
            using var response = await host.Client.GetAsync(route);
            var trace = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("traceId").GetString()!;
            Assert.Matches("^[0-9a-f]{32}$", trace);
            Assert.NotEqual("11111111111111111111111111111111", trace);
            Assert.Contains(host.Logs.Entries, entry => entry.Message.Contains(trace, StringComparison.Ordinal));
        }
    }

    [Theory]
    [InlineData("/missing", "GET", null, 404, "NOT_FOUND")]
    [InlineData("/error-probe/binding", "GET", null, 405, "METHOD_NOT_ALLOWED")]
    [InlineData("/error-probe/binding", "POST", "text/plain", 415, "UNSUPPORTED_MEDIA_TYPE")]
    [InlineData("/error-probe/binding", "POST", "application/json", 400, "VALIDATION_ERROR")]
    [InlineData("/error-probe/secure", "GET", null, 401, "UNAUTHORIZED")]
    [InlineData("/error-probe/domain-conflict", "GET", null, 409, "ORDER_ALREADY_PAID")]
    [InlineData("/error-probe/payos-secret-leak", "GET", null, 502, "PAYOS_500")]
    [InlineData("/error-probe/controller-error", "GET", null, 400, "MISSING_SESSION_ID")]
    [InlineData("/error-probe/db-concurrency", "GET", null, 409, "RESOURCE_VERSION_CONFLICT")]
    public async Task FrameworkAndApplicationFailuresUseApprovedStatusAndEnvelope(
        string route, string method, string? contentType, int status, string code)
    {
        await using var host = await PaymentErrorTestHost.StartAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), route);
        request.Headers.Add("X-Correlation-Id", "secret-client-controlled-correlation");
        if (contentType is not null)
        {
            request.Content = new StringContent("{\"name\":", Encoding.UTF8, contentType);
        }

        using var response = await host.Client.SendAsync(request);
        await AssertErrorAsync(response, status, code);

        var payloadText = await response.Content.ReadAsStringAsync();
        var trace = JsonDocument.Parse(payloadText).RootElement.GetProperty("traceId").GetString()!;
        Assert.Matches("^[0-9a-f]{32}$", trace);
        Assert.Contains(host.Logs.Entries, entry => entry.Message.Contains(trace, StringComparison.Ordinal));
        Assert.DoesNotContain("secret-client-controlled-correlation", payloadText, StringComparison.Ordinal);

        if (status == 401)
        {
            Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
            Assert.Equal(1, host.Events.Challenges);
        }

        if (status == 405)
        {
            Assert.Contains("POST", response.Content.Headers.Allow);
        }
    }

    [Fact]
    public async Task ValidBearerForbidAndInvalidBearerChallengeRetainExistingEvents()
    {
        await using var host = await PaymentErrorTestHost.StartAsync();
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", "secret-invalid-token");
        using (var challenge = await host.Client.GetAsync("/error-probe/secure"))
        {
            await AssertErrorAsync(challenge, 401, "UNAUTHORIZED");
        }

        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", host.CreateUserToken("DRIVER"));
        using var forbid = await host.Client.GetAsync("/error-probe/secure");
        await AssertErrorAsync(forbid, 403, "FORBIDDEN");

        Assert.Equal(1, host.Events.Validated);
        Assert.Equal(1, host.Events.Forbidden);
        Assert.Equal(1, host.Events.Challenges);
    }

    [Fact]
    public async Task OperationsServiceSchemeUsesUniform401And403Envelopes()
    {
        await using var host = await PaymentErrorTestHost.StartAsync();

        using (var unauthenticated = await host.Client.GetAsync("/error-probe/s2s-secure"))
        {
            await AssertOperationsChallengeAsync(unauthenticated, 401, "UNAUTHENTICATED");
            Assert.Equal("Bearer", Assert.Single(unauthenticated.Headers.WwwAuthenticate).Scheme);
        }

        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", host.CreateOperationsToken(includePermission: false));
        using (var forbidden = await host.Client.GetAsync("/error-probe/s2s-secure"))
        {
            await AssertOperationsChallengeAsync(forbidden, 403, "FORBIDDEN");
        }

        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", host.CreateOperationsToken(includePermission: true));
        using var authorized = await host.Client.GetAsync("/error-probe/s2s-secure");
        Assert.Equal(HttpStatusCode.NoContent, authorized.StatusCode);
    }

    private static async Task AssertOperationsChallengeAsync(HttpResponseMessage response, int status, string code)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);

        var text = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(text);
        var body = document.RootElement;
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.True(body.TryGetProperty("message", out _));
        Assert.True(body.TryGetProperty("traceId", out _));
    }

    [Fact]
    public async Task AlreadyHandledAuthenticationCallbacksAreNotOverwritten()
    {
        await using var host = await PaymentErrorTestHost.StartAsync(completeAuthenticationResponses: true);
        using var challenge = await host.Client.GetAsync("/error-probe/secure");
        Assert.Equal(HttpStatusCode.Unauthorized, challenge.StatusCode);
        Assert.Equal("custom challenge", await challenge.Content.ReadAsStringAsync());

        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", host.CreateUserToken("DRIVER"));
        using var forbid = await host.Client.GetAsync("/error-probe/secure");
        Assert.Equal(HttpStatusCode.Forbidden, forbid.StatusCode);
        Assert.Equal("custom forbid", await forbid.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task RateLimiterRejectionUsesSharedEnvelope()
    {
        await using var host = await PaymentErrorTestHost.StartAsync();
        using var first = await host.Client.GetAsync("/error-probe/limited");
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        using var second = await host.Client.GetAsync("/error-probe/limited");
        await AssertErrorAsync(second, 429, "TOO_MANY_REQUESTS");
    }

    [Theory]
    [InlineData("{\"name\":", 0)]
    [InlineData("{\"name\":123}", 0)]
    [InlineData("{\"name\":\"\"}", 1)]
    public async Task BindingAndRealMediatorValidationRejectBeforeHandler(string json, int validatorCalls)
    {
        await using var host = await PaymentErrorTestHost.StartAsync();
        using var response = await host.Client.PostAsync(
            "/error-probe/binding",
            new StringContent(json, Encoding.UTF8, "application/json"));

        await AssertErrorAsync(response, 400, "VALIDATION_ERROR");
        Assert.Equal(validatorCalls, host.Events.ValidatedInputs);
        Assert.Equal(0, host.Events.HandledInputs);
    }

    [Fact]
    public async Task UnexpectedDatabaseErrorIsSafe500LoggedOnceWithMetadataOnly()
    {
        await using var host = await PaymentErrorTestHost.StartAsync();
        using var response = await host.Client.GetAsync("/error-probe/unexpected/secret-route-value?password=secret-query");
        await AssertErrorAsync(response, 500, "INTERNAL_SERVER_ERROR");

        var errors = host.Logs.Entries.Where(entry => entry.Level == LogLevel.Error).ToArray();
        var error = Assert.Single(errors);
        Assert.Equal(typeof(PaymentExceptionHandler).FullName, error.Category);
        Assert.Null(error.Exception);

        var completion = Assert.Single(
            host.Logs.Entries,
            entry => entry.Category == typeof(PaymentRequestCompletionMiddleware).FullName);
        Assert.Contains("error-probe/unexpected/{id}", completion.Message, StringComparison.Ordinal);
        Assert.Contains("status 500", completion.Message, StringComparison.Ordinal);
        Assert.Contains("correlation", completion.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "secret",
            string.Join(" ", host.Logs.Entries.Select(entry => entry.Message)),
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HeadAndNoContentHaveNoBodyAndPdfAndStreamsAreUnchanged()
    {
        await using var host = await PaymentErrorTestHost.StartAsync();
        using var head = await host.Client.SendAsync(new(HttpMethod.Head, "/missing"));
        Assert.Equal(HttpStatusCode.NotFound, head.StatusCode);
        Assert.Empty(await head.Content.ReadAsByteArrayAsync());

        using var failedHead = await host.Client.SendAsync(new(HttpMethod.Head, "/head-failure"));
        Assert.Equal(HttpStatusCode.InternalServerError, failedHead.StatusCode);
        Assert.Empty(await failedHead.Content.ReadAsByteArrayAsync());

        using var noContent = await host.Client.GetAsync("/error-probe/empty");
        Assert.Equal(HttpStatusCode.NoContent, noContent.StatusCode);
        Assert.Empty(await noContent.Content.ReadAsByteArrayAsync());

        using var pdf = await host.Client.GetAsync("/error-probe/pdf");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.Equal("%PDF-1.4"u8.ToArray(), await pdf.Content.ReadAsByteArrayAsync());

        using var stream = await host.Client.GetAsync("/stream");
        Assert.Equal("text/event-stream", stream.Content.Headers.ContentType?.MediaType);
        Assert.Equal("data: first\n\ndata: second\n\n", await stream.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ResponseStartedFailurePropagatesWithoutAppendingAnErrorDocument()
    {
        await using var host = await PaymentErrorTestHost.StartAsync();
        var exception = await Record.ExceptionAsync(async () =>
        {
            using var response = await host.Client.GetAsync("/started", HttpCompletionOption.ResponseHeadersRead);
            await response.Content.ReadAsStringAsync();
        });

        Assert.NotNull(exception);
        Assert.DoesNotContain(host.Logs.Entries, entry => entry.Category == typeof(PaymentExceptionHandler).FullName);
        Assert.Contains(
            host.Logs.Entries,
            entry => entry.Category == typeof(PaymentRequestCompletionMiddleware).FullName &&
                     entry.Message.Contains("propagated failure True", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RequestCancellationHasNoGeneric500OrUnexpectedDiagnostic()
    {
        await using var host = await PaymentErrorTestHost.StartAsync();
        using var response = await host.Client.GetAsync("/cancelled");
        Assert.Equal(499, (int)response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        Assert.DoesNotContain(host.Logs.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task CancellationWithoutRequestAbortRemainsAnUnexpectedServerFailure()
    {
        await using var host = await PaymentErrorTestHost.StartAsync();
        using var response = await host.Client.GetAsync("/unexpected-cancellation");
        await AssertErrorAsync(response, 500, "INTERNAL_SERVER_ERROR");
        Assert.Single(host.Logs.Entries, entry => entry.Level == LogLevel.Error);
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, int status, string code)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoStore);

        var text = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(text);
        var body = document.RootElement;
        Assert.False(body.GetProperty("success").GetBoolean());
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Equal(code, body.GetProperty("errorCode").GetString());
        Assert.True(body.TryGetProperty("message", out _));
        Assert.True(body.TryGetProperty("traceId", out _));
        Assert.False(body.TryGetProperty("status", out _));
        Assert.False(body.TryGetProperty("statusCode", out _));
        Assert.False(body.TryGetProperty("title", out _));
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
    }
}
