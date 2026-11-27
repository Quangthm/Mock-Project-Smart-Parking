using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SmartParking.PaymentService.API.Authentication;
using SmartParking.PaymentService.API.Common;
using SmartParking.PaymentService.API.Middleware;

namespace SmartParking.PaymentService.API.Extensions;

public static class PaymentApiErrorExtensions
{
    public static IServiceCollection AddPaymentApiErrorResponses(this IServiceCollection services)
    {
        services.AddSingleton(PaymentExceptionMapper.CreateDefault());
        services.AddSingleton<PaymentApiErrorWriter>();
        services.AddExceptionHandler<PaymentExceptionHandler>();

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
                        new PaymentErrorDetail(entry.Key, "The supplied value is not valid.")))
                    .ToArray();
                return new PaymentApiErrorActionResult(PaymentErrorResponse.ValidationError(errors));
            };
        });

        PostConfigureJwtBearer(services, JwtBearerDefaults.AuthenticationScheme);
        PostConfigureJwtBearer(services, OperationsServiceAuthentication.Scheme);

        return services;
    }

    public static IApplicationBuilder UsePaymentApiErrorResponses(this IApplicationBuilder app)
    {
        app.UseMiddleware<PaymentRequestCompletionMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages(async statusContext =>
        {
            var httpContext = statusContext.HttpContext;
            if (PaymentErrorResponse.TryForStatus(httpContext.Response.StatusCode, out var response))
            {
                await httpContext.RequestServices.GetRequiredService<PaymentApiErrorWriter>()
                    .TryWriteAsync(httpContext, response, httpContext.RequestAborted);
            }
        });
        return app;
    }

    private static void PostConfigureJwtBearer(IServiceCollection services, string scheme)
    {
        services.PostConfigure<JwtBearerOptions>(scheme, options =>
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
                if (PaymentErrorResponse.TryForStatus(StatusCodes.Status401Unauthorized, out var response))
                {
                    await context.HttpContext.RequestServices.GetRequiredService<PaymentApiErrorWriter>()
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

                if (PaymentErrorResponse.TryForStatus(StatusCodes.Status403Forbidden, out var response))
                {
                    await context.HttpContext.RequestServices.GetRequiredService<PaymentApiErrorWriter>()
                        .TryWriteAsync(context.HttpContext, response, context.HttpContext.RequestAborted);
                }
            };
        });
    }
}
