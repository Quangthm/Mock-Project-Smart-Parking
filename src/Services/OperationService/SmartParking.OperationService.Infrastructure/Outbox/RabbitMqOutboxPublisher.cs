using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using SmartParking.OperationService.Application.Common.Interfaces.Services;

namespace SmartParking.OperationService.Infrastructure.Outbox;

public sealed class RabbitMqOutboxOptions
{
    public string? ConnectionString { get; set; }
    public string Exchange { get; set; } = "smartpark.occupancy";
}

public sealed class RabbitMqOutboxPublisher(IOptions<RabbitMqOutboxOptions> options) : IOutboxEventPublisher, IAsyncDisposable
{
    private readonly RabbitMqOutboxOptions _options = options.Value;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;
    private IChannel? _channel;

    public string Name => "rabbitmq";

    public async Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var channel = await GetChannelAsync(cancellationToken);
        var routingKey = ToRoutingKey(message.EventType);
        var body = JsonSerializer.SerializeToUtf8Bytes(new
        {
            eventId = message.EventId,
            eventType = message.EventType,
            occurredAt = message.OccurredAt,
            sequenceNumber = message.SequenceNumber,
            siteId = message.SiteId,
            payload = ParsePayload(message.Payload)
        });
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            MessageId = message.EventId.ToString(),
            Type = message.EventType,
            Persistent = true,
            Timestamp = new AmqpTimestamp(message.OccurredAt.ToUnixTimeSeconds())
        };
        await channel.BasicPublishAsync(_options.Exchange, routingKey, mandatory: false, properties, body, cancellationToken);
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true }) return _channel;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_channel is { IsOpen: true }) return _channel;
            if (_connection is not { IsOpen: true })
            {
                var factory = new ConnectionFactory { Uri = new Uri(_options.ConnectionString!) };
                _connection = await factory.CreateConnectionAsync(cancellationToken);
            }
            var channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
            await channel.ExchangeDeclareAsync(_options.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
            _channel = channel;
            return channel;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static object? ParsePayload(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return null;
        try
        {
            using var document = JsonDocument.Parse(payload);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return payload;
        }
    }

    public static string ToRoutingKey(string eventType)
    {
        var builder = new StringBuilder(eventType.Length + 4);
        for (var i = 0; i < eventType.Length; i++)
        {
            var c = eventType[i];
            if (char.IsUpper(c))
            {
                if (i > 0) builder.Append('.');
                builder.Append(char.ToLowerInvariant(c));
            }
            else
            {
                builder.Append(c);
            }
        }
        return builder.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null) await _channel.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
        _gate.Dispose();
    }
}
