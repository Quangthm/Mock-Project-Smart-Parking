using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Tests;

internal sealed class ReservationTestSubscription : IDisposable
{
    private readonly ClientWebSocket socket = new();
    private readonly Queue<JsonElement> messages = new();
    private readonly CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));

    public async Task<bool> SubscribeAsync(Uri server, Guid account, Guid request)
    {
        socket.Options.SetRequestHeader("Test-Account", account.ToString());
        await socket.ConnectAsync(new UriBuilder(server) { Scheme = "ws", Path = "/hubs/reservation-progress" }.Uri, timeout.Token);
        await SendAsync(new { protocol = "json", version = 1 });
        var handshake = await ReadAsync();
        Assert.False(handshake.TryGetProperty("error", out _));
        await SendAsync(new { type = 1, invocationId = "subscribe", target = "Subscribe", arguments = new[] { request } });
        while (true)
        {
            var reply = await ReadAsync();
            if (reply.TryGetProperty("invocationId", out var id) && id.GetString() == "subscribe")
                return !reply.TryGetProperty("error", out _);
        }
    }

    public async Task<ReservationProgress> UntilTerminalAsync(Guid request)
    {
        while (true)
        {
            var next = await ReadAsync();
            if (!next.TryGetProperty("target", out var target) || target.GetString() != "ReservationProgress") continue;
            var update = next.GetProperty("arguments")[0].Deserialize<ReservationProgress>(JsonSerializerOptions.Web)!;
            Assert.Equal(request, update.RequestId);
            Assert.NotNull(update.EventId);
            if (update.AdmissionState == "FAILED_DEPENDENCY") return update;
        }
    }

    private Task SendAsync(object payload)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload) + (char)30);
        return socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, timeout.Token);
    }

    private async Task<JsonElement> ReadAsync()
    {
        if (messages.TryDequeue(out var next)) return next;
        using var frame = new MemoryStream();
        var buffer = new byte[4096];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token);
            Assert.Equal(WebSocketMessageType.Text, result.MessageType);
            frame.Write(buffer, 0, result.Count);
        } while (!result.EndOfMessage);
        foreach (var json in Encoding.UTF8.GetString(frame.ToArray()).Split((char)30, StringSplitOptions.RemoveEmptyEntries))
            messages.Enqueue(JsonSerializer.Deserialize<JsonElement>(json));
        return messages.Dequeue();
    }

    public void Dispose()
    {
        socket.Dispose();
        timeout.Dispose();
    }
}
