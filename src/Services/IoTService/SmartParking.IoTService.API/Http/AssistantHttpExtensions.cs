using Microsoft.AspNetCore.Mvc;

namespace SmartParking.IoTService.API.Http;

public static class AssistantHttpExtensions
{
    public static IServiceCollection AddAssistantHttp(this IServiceCollection services)
    {
        services.AddLogging(logging =>
        {
            // Framework request/auth diagnostics may include raw URLs, token details or exception messages.
            logging.AddFilter("Microsoft.AspNetCore", LogLevel.None);
            logging.AddFilter("Microsoft.IdentityModel", LogLevel.None);
        });
        services.AddExceptionHandler<AssistantExceptionHandler>();
        services.AddControllers().AddApplicationPart(typeof(AssistantHttpExtensions).Assembly)
            .AddJsonOptions(options => options.JsonSerializerOptions.NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict)
            .ConfigureApiBehaviorOptions(options =>
            {
                options.SuppressMapClientErrors = true;
                options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(
                    AssistantErrors.Envelope(context.HttpContext, AssistantErrors.ForStatus(400)));
            });
        return services;
    }

    public static IApplicationBuilder UseAssistantHttp(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            context.TraceIdentifier = Guid.NewGuid().ToString("N");
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.CacheControl = "no-store";
                context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
                return Task.CompletedTask;
            });
            await next(context);
        });
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            ExceptionHandler = context =>
            {
                // Only reached when registered handlers decline (for example an aborted request).
                var exception = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
                if (exception is not null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception).Throw();
                return Task.CompletedTask;
            },
            // AssistantExceptionHandler owns sanitized unexpected-error logging.
            SuppressDiagnosticsCallback = _ => true
        });
        app.UseStatusCodePages(context => AssistantErrors.WriteAsync(context.HttpContext,
            AssistantErrors.ForStatus(context.HttpContext.Response.StatusCode)));
        return app;
    }
}
