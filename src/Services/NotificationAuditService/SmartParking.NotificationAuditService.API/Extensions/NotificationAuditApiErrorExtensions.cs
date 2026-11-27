using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SmartParking.NotificationAuditService.API.Common;
using SmartParking.NotificationAuditService.API.Middleware;

namespace SmartParking.NotificationAuditService.API.Extensions;

public static class NotificationAuditApiErrorExtensions
{
    public static IServiceCollection AddNotificationAuditApiErrorResponses(this IServiceCollection services)
    {
        services.AddSingleton(NotificationAuditExceptionMapper.CreateDefault());
        services.AddSingleton<NotificationApiErrorWriter>();
        services.AddExceptionHandler<NotificationAuditExceptionHandler>();

        services.Configure<ExceptionHandlerOptions>(options =>
        {
            options.SuppressDiagnosticsCallback = context =>
                context.ExceptionHandledBy == ExceptionHandledType.ExceptionHandlerService;
            options.ExceptionHandler = context =>
            {
                var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
                if (exception is not null)
                {
                    ExceptionDispatchInfo.Capture(exception).Throw();
                }

                return Task.CompletedTask;
            };
        });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.SuppressMapClientErrors = true;
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = context.ModelState
                    .Where(entry => entry.Value is { Errors.Count: > 0 })
                    .SelectMany(entry => entry.Value!.Errors.Select(_ =>
                        new NotificationErrorDetail(entry.Key, "The supplied value is not valid.")))
                    .ToArray();
                return new NotificationApiErrorActionResult(NotificationErrorResponse.ValidationError(errors));
            };
        });

        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            options.Events ??= new JwtBearerEvents();
            var previousChallenge = options.Events.OnChallenge;
            options.Events.OnChallenge = async context =>
            {
                if (previousChallenge is not null)
                {
                    await previousChallenge(context);
                }

                if (context.Handled || context.Response.HasStarted)
                {
                    return;
                }

                context.HandleResponse();
                if (NotificationErrorResponse.TryForStatus(StatusCodes.Status401Unauthorized, out var response))
                {
                    await context.HttpContext.RequestServices.GetRequiredService<NotificationApiErrorWriter>()
                        .TryWriteAsync(context.HttpContext, response, context.HttpContext.RequestAborted);
                }
            };

            var previousForbidden = options.Events.OnForbidden;
            options.Events.OnForbidden = async context =>
            {
                if (previousForbidden is not null)
                {
                    await previousForbidden(context);
                }

                if (context.Response.HasStarted)
                {
                    return;
                }

                if (NotificationErrorResponse.TryForStatus(StatusCodes.Status403Forbidden, out var response))
                {
                    await context.HttpContext.RequestServices.GetRequiredService<NotificationApiErrorWriter>()
                        .TryWriteAsync(context.HttpContext, response, context.HttpContext.RequestAborted);
                }
            };
        });

        return services;
    }

    public static IApplicationBuilder UseNotificationAuditApiErrorResponses(this IApplicationBuilder app)
    {
        app.UseMiddleware<NotificationRequestCompletionMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages(async statusContext =>
        {
            var httpContext = statusContext.HttpContext;
            if (NotificationErrorResponse.TryForStatus(httpContext.Response.StatusCode, out var response))
            {
                await httpContext.RequestServices.GetRequiredService<NotificationApiErrorWriter>()
                    .TryWriteAsync(httpContext, response, httpContext.RequestAborted);
            }
        });
        return app;
    }
}
