using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Application.Usecases.Occupancy.Commands.ApplyOccupancyProjection;

namespace SmartParking.FacilityService.Infrastructure;

public sealed class RabbitMqProjectionOptions
{
    public string? ConnectionString { get; set; }
    public string Exchange { get; set; } = "smartpark.occupancy";
    public string Queue { get; set; } = "smartpark.facility.occupancy";
    public int PrefetchCount { get; set; } = 16;
}

// Transport adapter only: consumes occupancy events published by OperationService and hands each
// one to IOccupancyProjectionStore (Persistence), which owns the database idempotency guarantees
// (delta design G5). This worker holds no SQL or database dependency.
public sealed class OccupancyProjectionWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqProjectionOptions> options,
    ILogger<OccupancyProjectionWorker> logger) : BackgroundService
{
    private readonly RabbitMqProjectionOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsumeAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogWarning(error, "Occupancy projection consumer disconnected; reconnecting in 5s.");
                try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }

    private async Task ConsumeAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            Uri = new Uri(_options.ConnectionString!),
            AutomaticRecoveryEnabled = true
        };
        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        await channel.ExchangeDeclareAsync(_options.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: stoppingToken);
        await channel.QueueDeclareAsync(_options.Queue, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await channel.QueueBindAsync(_options.Queue, _options.Exchange, routingKey: "#", cancellationToken: stoppingToken);
        await channel.BasicQosAsync(0, (ushort)Math.Max(1, _options.PrefetchCount), global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, delivery) =>
        {
            try
            {
                var projection = Parse(delivery.Body);
                using var scope = scopeFactory.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var applied = await mediator.Send(new ApplyOccupancyProjectionCommand(projection), stoppingToken);
                if (!applied) logger.LogDebug("Occupancy event was already applied; acknowledging without projection.");
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutting down: leave the delivery unacked so the broker redelivers it.
            }
            catch (JsonException error)
            {
                // Malformed JSON can never succeed on redelivery; discard the poison pill.
                logger.LogError(error, "Occupancy event could not be parsed; discarding delivery.");
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
            }
            catch (ValidationException error)
            {
                // A structurally invalid event is permanently malformed; do not requeue it.
                logger.LogError(error, "Occupancy event failed validation; discarding delivery.");
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
            }
            catch (Exception error)
            {
                logger.LogError(error, "Occupancy event projection failed; requeueing delivery.");
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: true, stoppingToken);
            }
        };
        await channel.BasicConsumeAsync(_options.Queue, autoAck: false, consumer, stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private static OccupancyProjectionEvent Parse(ReadOnlyMemory<byte> body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        var payload = root.TryGetProperty("payload", out var element) && element.ValueKind == JsonValueKind.Object
            ? element
            : default;

        return new OccupancyProjectionEvent(
            root.GetProperty("eventId").GetGuid(),
            root.GetProperty("eventType").GetString() ?? string.Empty,
            root.GetProperty("occurredAt").GetDateTimeOffset(),
            root.GetProperty("sequenceNumber").GetInt64(),
            root.GetProperty("siteId").GetGuid(),
            TryGuid(payload, "slotId"),
            TryGuid(payload, "sessionId"),
            TryString(payload, "physicalState"));
    }

    private static Guid? TryGuid(JsonElement payload, string name) =>
        TryGuid(payload, name, out var value) ? value : null;

    private static bool TryGuid(JsonElement payload, string name, out Guid value)
    {
        value = default;
        return payload.ValueKind == JsonValueKind.Object
            && payload.TryGetProperty(name, out var element)
            && element.ValueKind == JsonValueKind.String
            && Guid.TryParse(element.GetString(), out value);
    }

    private static string? TryString(JsonElement payload, string name) =>
        payload.ValueKind == JsonValueKind.Object
        && payload.TryGetProperty(name, out var element)
        && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
}
