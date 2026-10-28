using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Infrastructure.Adapters.Outbound;

public sealed class ReservationSnapshotHttpAdapter : IReservationPricingSnapshotPort
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ReservationSnapshotHttpAdapter> _logger;

    public ReservationSnapshotHttpAdapter(HttpClient httpClient, ILogger<ReservationSnapshotHttpAdapter> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PricingPolicySnapshot?> GetAcceptedPolicySnapshotAsync(
        Guid tenantId,
        Guid siteId,
        Guid reservationId,
        CancellationToken cancellationToken = default)
    {
        var url = $"internal/reservations/{reservationId}/pricing-snapshot?tenantId={tenantId}&siteId={siteId}";

        try
        {
            using var response = await _httpClient.GetAsync(url, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<PricingPolicySnapshot>(
                cancellationToken: cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to call ReservationService at {Url}", url);
            throw new PricingDomainException("RESERVATION_SERVICE_UNAVAILABLE", "Reservation service is temporarily unavailable.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize snapshot from ReservationService");
            throw new PricingDomainException("CORRUPTED_POLICY_SNAPSHOT", "Invalid snapshot format from Reservation service.", "reservationId");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError("Timeout calling ReservationService at {Url}", url);
            throw new PricingDomainException("RESERVATION_SERVICE_TIMEOUT", "Reservation service request timed out.");
        }
    }
}
