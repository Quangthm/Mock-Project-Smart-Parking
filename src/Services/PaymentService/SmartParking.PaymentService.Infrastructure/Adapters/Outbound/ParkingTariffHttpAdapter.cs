using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Infrastructure.Adapters.Outbound;

public sealed class ParkingTariffHttpAdapter : IParkingTariffPort
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ParkingTariffHttpAdapter> _logger;

    public ParkingTariffHttpAdapter(HttpClient httpClient, ILogger<ParkingTariffHttpAdapter> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PricingPolicySnapshot?> GetActivePolicyAsync(
        Guid tenantId,
        Guid siteId,
        PricingVehicleType vehicleType,
        CancellationToken cancellationToken = default)
    {
        var url = $"internal/tariffs/active?tenantId={tenantId}&siteId={siteId}&vehicleType={vehicleType}";

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
            _logger.LogError(ex, "Failed to call ParkingService at {Url}", url);
            throw new PricingDomainException("PARKING_SERVICE_UNAVAILABLE", "Parking service is temporarily unavailable.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize tariff from ParkingService");
            throw new PricingDomainException("INVALID_REMOTE_RESPONSE", "Invalid tariff format from Parking service.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogError("Timeout calling ParkingService at {Url}", url);
            throw new PricingDomainException("PARKING_SERVICE_TIMEOUT", "Parking service request timed out.");
        }
    }
}
