using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using UserService.API.Middleware;
using UserService.API.Common;
using Microsoft.Extensions.DependencyInjection;
using UserService.Application.Common.Behaviors;
using UserService.Application.Common.Models.Exceptions;

namespace SmartParking.UserService.Tests;

public sealed class IdentityArchitectureTests
{
    [Theory]
    [InlineData("MFA_CHALLENGE_INVALID", 401)]
    [InlineData("MFA_SETUP_EXPIRED", 400)]
    [InlineData("MFA_NOT_ENABLED", 409)]
    [InlineData("MFA_LOCKED", 423)]
    [InlineData("SETUP_TOKEN_INVALID", 401)]
    [InlineData("SETUP_ALREADY_SAVED", 409)]
    [InlineData("APPROVAL_CONFLICT", 409)]
    [InlineData("APPROVAL_IN_PROGRESS", 409)]
    [InlineData("LOT_SETUP_REQUIRED", 409)]
    [InlineData("OPERATOR_NOT_FOUND", 404)]
    public void IncomingWorkflowErrorsRetainPublicStatusWithoutReflectingExceptionText(string code, int status)
    {
        var result = ApiExceptionMapper.CreateDefault().Map(new AuthException(code, "private exception content", status));
        Assert.Equal(status, result.Response.StatusCode);
        var body = JsonSerializer.Serialize(result.Response.Body);
        Assert.Contains(code, body);
        Assert.DoesNotContain("private exception content", body);
        Assert.False(result.IsUnexpected);
    }

    private sealed class AsyncRule : AbstractValidator<string>
    {
        public AsyncRule(List<string> calls, string name)
        {
            RuleFor(x => x).MustAsync(async (_, ct) =>
            {
                calls.Add(name + "-start");
                await Task.Delay(5,ct);
                calls.Add(name + "-end");
                return true;
            });
        }
    }
    [Fact]
    public async Task PipelineAwaitsAsyncValidatorsSequentially()
    {
        var calls = new List<string>();
        var pipeline = new ValidationBehavior<string,string>([new AsyncRule(calls,"first"),new AsyncRule(calls,"second")]);
        Assert.Equal("handled",await pipeline.Handle("valid",_=>Task.FromResult("handled"),default));
        Assert.Equal(new[]{"first-start","first-end","second-start","second-end"},calls);
    }
    [Theory]
    [InlineData("unexpected",500,"INTERNAL_ERROR")]
    [InlineData("business",403,"FORBIDDEN")]
    [InlineData("unauthorized",500,"INTERNAL_ERROR")]
    public async Task GlobalBoundaryPreservesContractsAndSanitizesUnexpectedErrors(string type,int status,string code)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        using var services = new ServiceCollection().AddLogging().AddOptions().BuildServiceProvider();
        context.RequestServices = services;
        Exception exception = type switch
        {
            "business" => new AuthException("FORBIDDEN","Current permission required.",403),
            "unauthorized" => new UnauthorizedAccessException("secret connection string"),
            _ => new InvalidOperationException("secret connection string")
        };
        var handler = new ApiExceptionHandler(ApiExceptionMapper.CreateDefault(), new ApiErrorWriter(), NullLogger<ApiExceptionHandler>.Instance);
        Assert.True(await handler.TryHandleAsync(context, exception, default));
        Assert.Equal(status,context.Response.StatusCode);
        var json=Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
        Assert.DoesNotContain("secret",json);
        Assert.Equal(code,JsonDocument.Parse(json).RootElement.GetProperty("code").GetString());
    }
}
