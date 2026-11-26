using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SmartParking.IoTService.Application.Assistant;

public sealed class AssistantStageMetrics(IMeterFactory meters)
{
    private readonly Histogram<double> duration = meters.Create("SmartPark.Assistant")
        .CreateHistogram<double>("smartpark.assistant.stage.duration", "s");

    internal Measurement Start(string stage) => duration.Enabled ? new(duration, stage, Stopwatch.GetTimestamp()) : default;

    internal readonly record struct Measurement(Histogram<double>? Duration, string Stage, long Started) : IDisposable
    {
        public void Dispose() => Duration?.Record(Stopwatch.GetElapsedTime(Started).TotalSeconds,
            new KeyValuePair<string, object?>("smartpark.assistant.stage", Stage));
    }
}
