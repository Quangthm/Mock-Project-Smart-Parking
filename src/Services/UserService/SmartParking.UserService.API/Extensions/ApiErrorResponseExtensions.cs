using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using UserService.API.Common;
using UserService.API.Middleware;

namespace UserService.API.Extensions;

public static class ApiErrorResponseExtensions
{
    /// <summary>
    /// Routes the framework-generated failures through the shared JSON envelope:
    /// request binding errors, the authentication challenge and the authorization forbid.
    /// </summary>
    /// <remarks>
    /// The status codes and the challenge header are unchanged; only the body gains the
    /// envelope that every other UserService response already uses.
    /// </remarks>
    public static IServiceCollection AddApiErrorResponses(this IServiceCollection services)
    {
        services.AddSingleton(ApiExceptionMapper.CreateDefault());
        services.AddSingleton<ApiErrorWriter>();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.Configure<ExceptionHandlerOptions>(options =>
        {
            // IExceptionHandler owns safe diagnostics for handled failures. This
            // also makes .NET 10's diagnostics policy explicit for future upgrades.
            options.SuppressDiagnosticsCallback = context =>
                context.ExceptionHandledBy == ExceptionHandledType.ExceptionHandlerService;
            options.ExceptionHandler = async context =>
            {
                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                if (exception is not null)
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
                await Task.CompletedTask;
            };
        });
        services.Configure<ApiBehaviorOptions>(options =>
        {
            // Leave empty framework client-error results for the shared status
            // writer instead of allowing MVC to substitute ProblemDetails.
            options.SuppressMapClientErrors = true;
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(entry => entry.Value is { Errors.Count: > 0 })
                    .SelectMany(entry => entry.Value!.Errors.Select(error => new ApiErrorDetail(
                        entry.Key,
                        // Formatter details can include JSON values/internal type
                        // names. Public binding errors deliberately stay generic.
                        "The supplied value is not valid.")))
                    .ToArray();
                var response = ApiResponseFactory.ValidationError(errors);
                return new ApiErrorActionResult(response);
            };
        });

        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Events ??= new JwtBearerEvents();
            var previousChallenge = options.Events.OnChallenge;
            options.Events.OnChallenge = async context =>
            {
                if (previousChallenge is not null) await previousChallenge(context);
                if (context.Handled || context.Response.HasStarted) return;
                context.HandleResponse();
                ApiResponseFactory.TryForStatus(StatusCodes.Status401Unauthorized, out var response);
                await context.HttpContext.RequestServices.GetRequiredService<ApiErrorWriter>()
                    .TryWriteAsync(context.HttpContext, response, context.HttpContext.RequestAborted);
            };
            var previousForbidden = options.Events.OnForbidden;
            options.Events.OnForbidden = async context =>
            {
                if (previousForbidden is not null) await previousForbidden(context);
                if (context.Response.HasStarted) return;
                ApiResponseFactory.TryForStatus(StatusCodes.Status403Forbidden, out var response);
                await context.HttpContext.RequestServices.GetRequiredService<ApiErrorWriter>()
                    .TryWriteAsync(context.HttpContext, response, context.HttpContext.RequestAborted);
            };
        });

        return services;
    }

    public static IApplicationBuilder UseApiErrorResponses(this IApplicationBuilder app)
    {
        app.UseMiddleware<RequestCompletionMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages(async context =>
        {
            if (ApiResponseFactory.TryForStatus(context.HttpContext.Response.StatusCode, out var response))
                await context.HttpContext.RequestServices.GetRequiredService<ApiErrorWriter>()
                    .TryWriteAsync(context.HttpContext, response, context.HttpContext.RequestAborted);
        });
        return app;
    }
}
