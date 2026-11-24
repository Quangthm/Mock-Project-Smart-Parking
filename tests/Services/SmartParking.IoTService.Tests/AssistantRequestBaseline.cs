using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace SmartParking.IoTService.Tests;

public sealed partial class AuthenticatedChatTests(ITestOutputHelper output)
{
    private const string FaqRoute = "/api/assistant/guest/messages";
    private const string SendRoute = "/api/assistant/conversations/{id}/messages";
    private const string LookupRoute = "/api/assistant/conversations/{id}/requests/{key}";

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task RequestHistogramFixtureBaseline(int concurrency)
    {
        const int batches = 10;
        const int warmups = 2;
        const int samples = 8;
        var measurements = new ConcurrentQueue<(string Route, double Milliseconds, bool Failed)>();
        var invalidOutcomes = new ConcurrentQueue<string>();
        void Record(Instrument _, double seconds, ReadOnlySpan<KeyValuePair<string, object?>> tags, object? state)
        {
            string? route = null;
            var status = 0;
            var aborted = false;
            foreach (var (name, value) in tags)
            {
                if (name == "http.route") route = (string?)value;
                if (name == "http.response.status_code") status = (int)value!;
                if (name == "smartpark.assistant.request.aborted") aborted = (bool)value!;
            }
            if (route is FaqRoute or SendRoute or LookupRoute)
                measurements.Enqueue((route, seconds * 1000, status is < 200 or >= 300 || aborted));
        }

        for (var batch = 0; batch < batches; batch++)
        {
            // A fresh host per batch keeps warm-up + samples inside the guest limit of ten per socket IP.
            await using var fixture = await Fixture.Create(measure: true);
            var meter = fixture.Meter;
            await Exercise(warmups, fixture);
            using var listener = new MeterListener();
            listener.InstrumentPublished = (instrument, current) =>
            {
                if (instrument.Meter == meter && instrument.Name == "smartpark.assistant.request.duration")
                    current.EnableMeasurementEvents(instrument);
            };
            listener.SetMeasurementEventCallback<double>(Record);
            listener.Start();
            await Exercise(samples, fixture);
        }

        foreach (var route in new[] { FaqRoute, SendRoute, LookupRoute })
        {
            var entries = measurements.Where(value => value.Route == route).ToArray();
            Assert.Equal(batches * samples, entries.Length);
            var ordered = entries.Select(value => value.Milliseconds).Order().ToArray();
            var failures = entries.Count(value => value.Failed);
            output.WriteLine(FormattableString.Invariant(
                $"{route} concurrency={concurrency} n={entries.Length} warmup={batches * warmups} p50_ms={Percentile(0.50):F3} p95_ms={Percentile(0.95):F3} errors={failures}/{entries.Length} ({(double)failures / entries.Length:P1})"));
            double Percentile(double percentile) => ordered[(int)Math.Ceiling(ordered.Length * percentile) - 1];
        }
        Assert.DoesNotContain(measurements, value => value.Failed);
        Assert.Empty(invalidOutcomes);

        async Task Exercise(int count, Fixture fixture)
        {
            var targets = new List<(Guid Conversation, Guid Request, string Token)>();
            for (var index = 0; index < count; index++)
            {
                var token = fixture.Token(Guid.NewGuid());
                targets.Add((await fixture.Conversation(token: token), Guid.NewGuid(), token));
            }

            await Run(targets, _ => fixture.Post(FaqRoute,
                new { requestId = Guid.NewGuid(), message = "What is SmartPark?", locale = "en" }, token: ""), "outcome", "answered");
            await Run(targets, target => fixture.Post($"/api/assistant/conversations/{target.Conversation}/messages",
                new { clientMessageId = target.Request, expectedRevision = 0, message = "Tell me about astronomy" }, target.Token), "state", "completed");
            await Run(targets, target => fixture.Get($"/api/assistant/conversations/{target.Conversation}/requests/{target.Request}", target.Token),
                "state", "completed");
        }

        async Task Run(List<(Guid Conversation, Guid Request, string Token)> targets,
            Func<(Guid Conversation, Guid Request, string Token), Task<HttpResponseMessage>> send, string field, string expected)
        {
            foreach (var chunk in targets.Chunk(concurrency))
            {
                await Task.WhenAll(chunk.Select(async target =>
                {
                    using var response = await send(target);
                    if (!response.IsSuccessStatusCode) return;
                    var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
                    if (envelope.GetProperty("data").GetProperty(field).GetString() != expected)
                        invalidOutcomes.Enqueue(field);
                }));
            }
        }
    }
}
