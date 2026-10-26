using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using UserService.Infrastructure.Messaging;
using UserService.Persistence;

namespace SmartParking.UserService.Tests;

public sealed class ProfileBrokerFactAttribute : FactAttribute
{
    public ProfileBrokerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SMARTPARK_AUTH_TEST_CONNECTION"))
            || !int.TryParse(Environment.GetEnvironmentVariable("SMARTPARK_PROFILE_RABBITMQ_PORT"), out _))
            Skip = "Set the isolated PostgreSQL test connection and SMARTPARK_PROFILE_RABBITMQ_PORT (loopback broker) to run real delivery checks.";
    }
}

public sealed class ProfileOutboxBrokerTests
{
    [ProfileBrokerFact]
    public async Task BrokerOutagePreservesCommittedEventAndRecoveryRoutesItWithoutAConsumer()
    {
        var brokerPort = int.Parse(Environment.GetEnvironmentVariable("SMARTPARK_PROFILE_RABBITMQ_PORT")!);
        await using var fixture = await UserServiceDatabaseFixture.CreateAsync();
        var userId = await ProfileOutboxTests.SeedAsync(fixture);
        var message = ProfileOutboxTests.NewEvent(userId);

        // Independent provisioning occurs before the producer starts; there is no Audit consumer.
        var factory = new ConnectionFactory { HostName = "127.0.0.1", Port = brokerPort };
        await using var broker = await factory.CreateConnectionAsync();
        await using var channel = await broker.CreateChannelAsync();
        var queue = "smartpark_profile_test_" + Guid.NewGuid().ToString("N");
        await channel.ExchangeDeclareAsync(ProfileMessagingRegistration.ProfileNameChangedExchange, "fanout", true, false);
        await channel.QueueDeclareAsync(queue, true, false, false);
        await channel.QueueBindAsync(queue, ProfileMessagingRegistration.ProfileNameChangedExchange, "");

        try
        {
            // This gate closes only the producer's connection. The real broker stays running,
            // and the persisted DestinationAddress stays unchanged throughout recovery.
            await using var gate = new BrokerConnectionGate(brokerPort);
            var configuration = ProfileOutboxTests.Configuration(gate.Port, deliver: true);
            using var host = Host.CreateDefaultBuilder().ConfigureLogging(log => log.ClearProviders())
                .ConfigureServices(services =>
                {
                    services.AddDbContext<AppDbContext>(options => options.UseNpgsql(fixture.ConnectionString));
                    services.AddProfileMessaging(configuration, registration => registration.AddProfileOutbox(configuration));
                }).Build();
            await host.StartAsync().WaitAsync(TimeSpan.FromSeconds(10));
            try
            {
                await gate.ConnectionAttempt.WaitAsync(TimeSpan.FromSeconds(10));
                await ProfileOutboxTests.WriteAsync(host.Services, userId, "Saved during outage", message, commit: true)
                    .WaitAsync(TimeSpan.FromSeconds(10));
                await using (var db = fixture.Open())
                {
                    Assert.Equal("Saved during outage", (await db.Users.SingleAsync()).FullName);
                    var pending = await db.Set<OutboxMessage>().SingleAsync();
                    Assert.Equal(message.EventId, pending.MessageId);
                    Assert.Contains($":{gate.Port}/", pending.DestinationAddress!.ToString());
                }
                Assert.Equal(0u, (await channel.QueueDeclarePassiveAsync(queue)).ConsumerCount);
                Assert.Equal(0u, (await channel.QueueDeclarePassiveAsync(queue)).MessageCount);

                gate.Enable();
                await EventuallyAsync(async () => (await channel.QueueDeclarePassiveAsync(queue)).MessageCount > 0);
                var declaration = await channel.QueueDeclarePassiveAsync(queue);
                Assert.Equal(0u, declaration.ConsumerCount);
                var received = await channel.BasicGetAsync(queue, autoAck: false);
                Assert.NotNull(received);
                Assert.True(received.BasicProperties.Persistent);
                Assert.Equal(message.EventId, Guid.Parse(received.BasicProperties.MessageId!));
                using var payload = JsonDocument.Parse(received.Body);
                Assert.Equal(message.EventId, payload.RootElement.GetProperty("message").GetProperty("eventId").GetGuid());
                await channel.BasicAckAsync(received.DeliveryTag, multiple: false);
                await EventuallyAsync(async () =>
                {
                    await using var db = fixture.Open();
                    return !await db.Set<OutboxMessage>().AnyAsync() && !await db.Set<OutboxState>().AnyAsync();
                });
            }
            finally { await host.StopAsync().WaitAsync(TimeSpan.FromSeconds(15)); }
        }
        finally
        {
            Assert.Matches("^smartpark_profile_test_[0-9a-f]{32}$", queue);
            await channel.QueueDeleteAsync(queue, ifUnused: true, ifEmpty: false);
        }
    }

    private static async Task EventuallyAsync(Func<Task<bool>> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        while (!await condition()) await Task.Delay(100, deadline.Token);
    }

    private sealed class BrokerConnectionGate : IAsyncDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource shutdown = new();
        private readonly ConcurrentBag<Task> relays = [];
        private readonly TaskCompletionSource attempt = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly int targetPort;
        private readonly Task acceptLoop;
        private int enabled;
        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;
        public Task ConnectionAttempt => attempt.Task;

        public BrokerConnectionGate(int targetPort)
        {
            this.targetPort = targetPort;
            listener.Start();
            acceptLoop = AcceptAsync();
        }

        public void Enable() => Interlocked.Exchange(ref enabled, 1);

        private async Task AcceptAsync()
        {
            try
            {
                while (!shutdown.IsCancellationRequested)
                {
                    var client = await listener.AcceptTcpClientAsync(shutdown.Token);
                    attempt.TrySetResult();
                    if (Volatile.Read(ref enabled) == 0) client.Dispose();
                    else relays.Add(RelayAsync(client));
                }
            }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
        }

        private async Task RelayAsync(TcpClient client)
        {
            using (client)
            using (var target = new TcpClient())
            {
                try
                {
                    await target.ConnectAsync(IPAddress.Loopback, targetPort, shutdown.Token);
                    var sourceStream = client.GetStream();
                    var targetStream = target.GetStream();
                    var outgoing = sourceStream.CopyToAsync(targetStream, shutdown.Token);
                    var incoming = targetStream.CopyToAsync(sourceStream, shutdown.Token);
                    await Task.WhenAny(outgoing, incoming);
                    client.Close();
                    target.Close();
                    try { await Task.WhenAll(outgoing, incoming); }
                    catch (Exception error) when (error is IOException or OperationCanceledException or ObjectDisposedException) { }
                }
                catch (Exception error) when (error is IOException or SocketException or OperationCanceledException) { }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await shutdown.CancelAsync();
            listener.Stop();
            await acceptLoop;
            await Task.WhenAll(relays);
            shutdown.Dispose();
        }
    }
}
