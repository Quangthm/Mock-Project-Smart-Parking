using System.Diagnostics;
using System.Diagnostics.Metrics;
using SmartParking.IoTService.Application.Assistant.Knowledge;

namespace SmartParking.IoTService.Infrastructure.Assistant.Knowledge;

public static class ProviderTransport
{
    public const string ClientName = "assistant-generation";

    internal static Histogram<double>? CreateDuration(IMeterFactory? meters) => meters?.Create("SmartPark.Assistant")
        .CreateHistogram<double>("smartpark.assistant.provider.attempt.duration", "s");

    internal static async Task<GroundedReply> Generate(DemoGroundedOptions options, IHttpClientFactory? clients,
        Histogram<double>? duration, string message, CancellationToken ct)
    {
        var started = duration?.Enabled == true ? Stopwatch.GetTimestamp() : 0L;
        var outcome = "failed";
        try
        {
            using var client = clients?.CreateClient(ClientName);
            using var generator = client is null ? new DemoGroundedGenerator(options) : new DemoGroundedGenerator(options, client);
            var result = await generator.GenerateAsync(message, ct);
            outcome = "completed";
            return result;
        }
        catch (OperationCanceledException)
        {
            outcome = "cancelled";
            throw;
        }
        finally
        {
            if (started != 0)
                duration!.Record(Stopwatch.GetElapsedTime(started).TotalSeconds,
                    new KeyValuePair<string, object?>("smartpark.assistant.outcome", outcome));
        }
    }
}
