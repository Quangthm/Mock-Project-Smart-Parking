using System.Net.Http.Json;
using System.Text.Json;
using MassTransit;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Infrastructure.Reservations;

public sealed class ReservationVehicleClient(HttpClient client) : IReservationVehicleOwner
{
    private sealed record Envelope(bool Success, Binding? Data);
    private sealed record Binding(string State, Guid? VehicleId, Guid? AccountId, string? VehicleType, long? Revision, string? CanonicalPlate);
    public async Task<VehicleBindingResult> ResolveAsync(Guid accountId, Guid vehicleId, CancellationToken ct)
    {
        try
        {
            using var response = await client.PostAsJsonAsync("internal/vehicle-bindings/resolve",
                new { accountId, vehicleId, expectedRevision = (long?)null }, ct);
            if (!response.IsSuccessStatusCode) throw Unavailable();
            var body = await response.Content.ReadFromJsonAsync<Envelope>(cancellationToken: ct);
            if (body is not { Success: true, Data: not null }) throw Unavailable();
            if (body.Data.State == "INELIGIBLE") return new(false, null, null);
            if (body.Data.State != "ACTIVE" || body.Data.AccountId != accountId || body.Data.VehicleId != vehicleId ||
                body.Data.Revision is null or <= 0 || string.IsNullOrWhiteSpace(body.Data.CanonicalPlate) ||
                Domain.GateRules.NormalizePlate(body.Data.CanonicalPlate) != body.Data.CanonicalPlate) throw Unavailable();
            return new(body.Data.VehicleType == "CAR", body.Data.Revision, body.Data.CanonicalPlate);
        }
        catch (HttpRequestException) { throw Unavailable(); }
        catch (JsonException) { throw Unavailable(); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Unavailable(); }
    }
    private static ReservationGateException Unavailable() => new(503, "OWNER_UNAVAILABLE", "Vehicle binding authority is unavailable.");
}

public sealed class ReservationFactsPublisher(IPublishEndpoint publisher) : IReservationFactsPublisher
{
    public Task FacilityAppliedAsync(FacilityStructureAppliedV1 fact, CancellationToken ct) =>
        publisher.Publish(fact, context => { context.MessageId = fact.EventId; context.CorrelationId = fact.SynchronizationToken; }, ct);
    public async Task HoldChangedAsync(ReservationHoldChangedV1 fact, CancellationToken ct)
    {
        await publisher.Publish(fact, context =>
        { context.MessageId = fact.EventId; context.CorrelationId = fact.WorkflowId; }, ct);
        await AvailabilityChangedAsync(new ReservationAvailabilityChangedV1(Guid.NewGuid(), fact.OccurredAt,
            fact.TenantId, fact.SiteId, fact.SlotId, fact.Start, fact.End, fact.State), ct);
    }
    public Task AvailabilityChangedAsync(ReservationAvailabilityChangedV1 fact, CancellationToken ct) =>
        publisher.Publish(fact, context => { context.MessageId = fact.EventId; }, ct);
    public Task ConfirmedAsync(ReservationConfirmedV1 fact, CancellationToken ct) => publisher.Publish(fact, context =>
    { context.MessageId = fact.EventId; context.CorrelationId = fact.ReservationId; }, ct);
}
