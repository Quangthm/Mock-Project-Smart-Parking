using System.Text;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using UserService.API.Middleware;
using UserService.Application.Common.Behaviors;
using UserService.Application.Common.Models.Exceptions;

namespace SmartParking.UserService.Tests;

public sealed class IdentityArchitectureTests
{
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
    [InlineData("unauthorized",401,"INVALID_TOKEN")]
    public async Task GlobalBoundaryPreservesContractsAndSanitizesUnexpectedErrors(string type,int status,string code)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var middleware = new AuthExceptionMiddleware(_ => throw type switch
        {
            "business" => new AuthException("FORBIDDEN","Current permission required.",403),
            "unauthorized" => new UnauthorizedAccessException("secret connection string"),
            _ => new InvalidOperationException("secret connection string")
        }, NullLogger<AuthExceptionMiddleware>.Instance);
        await middleware.InvokeAsync(context);
        Assert.Equal(status,context.Response.StatusCode);
        var json=Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
        Assert.DoesNotContain("secret",json);
        Assert.Equal(code,JsonDocument.Parse(json).RootElement.GetProperty("code").GetString());
    }
}
