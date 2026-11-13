using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.AspNetCore.Routing;

namespace SmartParking.IoTService.API.Http;

internal sealed class AssistantRequestMetrics(RequestDelegate next, IMeterFactory meterFactory)
{
    private readonly Histogram<double> duration = meterFactory.Create("SmartPark.Assistant")
        .CreateHistogram<double>("smartpark.assistant.request.duration", "s");

    public async Task InvokeAsync(HttpContext context)
    {
        if (!duration.Enabled)
        {
            await next(context);
            return;
        }

        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            // Record only server-defined route templates. Unmatched URLs can contain private inputs.
            var tags = new TagList
            {
                { "http.route", (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText ?? "unmatched" },
                { "http.response.status_code", context.Response.StatusCode },
                { "smartpark.assistant.request.aborted", context.RequestAborted.IsCancellationRequested }
            };
            duration.Record(Stopwatch.GetElapsedTime(started).TotalSeconds, tags);
        }
    }
}
