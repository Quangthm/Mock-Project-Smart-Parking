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

// Real producer adapters replace these registrations only after their versioned/authenticated
// contracts are delivered. No legacy-price, fake coverage or demo payment fallback is permitted.
public sealed class UnavailableReservationCommercialOwner : IReservationCommercialOwner
{
    private static ReservationGateException Missing() => new(503, "OWNER_UNAVAILABLE", "Reservation commercial producer is not integrated.");
    public Task<PricePreview> PreviewAsync(Guid accountId, ReservationPricePreview intent, CancellationToken ct) => throw Missing();
    public Task<CommercialTerms> QuoteAsync(Guid accountId, BookingSelection intent, CancellationToken ct) => throw Missing();
    public Task<DepositOrderResult> EnsureOrderAsync(DepositOrderRequest request, CancellationToken ct) => throw Missing();
    public Task<ZeroPriceAcceptanceResult> AcceptZeroPriceAsync(ZeroPriceAcceptanceRequest request, CancellationToken ct) => throw Missing();
    public Task<CollectionLookup> CollectionsAsync(Guid reservationId, Guid orderId, CancellationToken ct) => throw Missing();
    public Task ReconcileAsync(Guid reservationId, VerifiedCollection collection, string reason, CancellationToken ct) => throw Missing();
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
