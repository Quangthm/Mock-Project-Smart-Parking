using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Infrastructure;

namespace SmartParking.FacilityService.Tests;

// Downstream structural failures must be diagnosable (operation, site, status code, truncated
// body) while still failing closed as COMMITMENTS_UNAVAILABLE. A deliberate 409 remains the
// distinct STRUCTURE_BUSY signal and must never be mislabeled as unavailable.
public sealed class StructureCommitmentsClientDiagnosticsTests
{
    [Fact]
    public async Task ReadSnapshotAsync_NonSuccess_ThrowsCommitmentsUnavailable()
    {
        var client = new StructureCommitmentsClient(Client(HttpStatusCode.InternalServerError, "boom"));

        var error = await Assert.ThrowsAsync<StructureException>(() =>
            client.ReadSnapshotAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("COMMITMENTS_UNAVAILABLE", error.Code);
    }

    [Fact]
    public async Task ReadSnapshotAsync_NonSuccess_LogsDownstreamStatusCode()
    {
        var logger = new CapturingLogger<StructureCommitmentsClient>();
        var client = new StructureCommitmentsClient(
            Client(HttpStatusCode.InternalServerError, "boom"), logger);

        await Assert.ThrowsAsync<StructureException>(() =>
            client.ReadSnapshotAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));

        Assert.Contains(logger.Entries, entry =>
            entry.Level == LogLevel.Error && entry.Message.Contains("500"));
    }

    [Fact]
    public async Task AcquireAsync_Conflict_StaysStructureBusy()
    {
        var client = new StructureCommitmentsClient(Client(HttpStatusCode.Conflict, null));

        var error = await Assert.ThrowsAsync<StructureException>(() =>
            client.AcquireAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("STRUCTURE_BUSY", error.Code);
        Assert.NotEqual("COMMITMENTS_UNAVAILABLE", error.Code);
    }

    [Fact]
    public async Task AcquireAsync_NonConflictFailure_ThrowsCommitmentsUnavailable()
    {
        var client = new StructureCommitmentsClient(Client(HttpStatusCode.BadGateway, "downstream"));

        var error = await Assert.ThrowsAsync<StructureException>(() =>
            client.AcquireAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));

        Assert.Equal("COMMITMENTS_UNAVAILABLE", error.Code);
    }

    private static HttpClient Client(HttpStatusCode status, string? body) =>
        new(new StubHandler(status, body)) { BaseAddress = new Uri("http://localhost/") };

    private sealed class StubHandler(HttpStatusCode status, string? body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(status);
            if (body is not null)
                response.Content = new StringContent(body, Encoding.UTF8, "text/plain");
            return Task.FromResult(response);
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }
}
