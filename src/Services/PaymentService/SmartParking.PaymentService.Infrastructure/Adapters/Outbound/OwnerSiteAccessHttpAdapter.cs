using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.DTOs.PassPlans;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Infrastructure.Adapters.Outbound.Models;

namespace SmartParking.PaymentService.Infrastructure.Adapters.Outbound;

public sealed class OwnerSiteAccessHttpAdapter(
    HttpClient userClient,
    HttpClient facilityClient,
    IHttpContextAccessor httpContextAccessor,
    string? internalServiceKey = null) : IOwnerSiteAccessPort
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OwnerSiteScopeResult> ValidateOwnerSiteAsync(Guid siteId, CancellationToken ct)
    {
        if (siteId == Guid.Empty)
        {
            throw new PaymentDomainException("SITE_NOT_FOUND", "Parking lot not found.", nameof(siteId));
        }

        var bearer = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(bearer))
        {
            throw new PaymentDomainException("UNAUTHENTICATED", "Owner authentication is required.");
        }

        if (!bearer.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            bearer = $"Bearer {bearer}";
        }

        var scope = await GetOwnerScopeAsync(bearer, ct);
        var tenantIds = scope.TenantIds ?? [];

        if (!string.IsNullOrWhiteSpace(internalServiceKey) && tenantIds.Count > 0)
        {
            var validated = await TryValidateSiteWithFacilityAsync(siteId, tenantIds, ct);
            if (validated is not null)
            {
                return new OwnerSiteScopeResult(scope.UserId, validated.TenantId, siteId);
            }
        }

        if (tenantIds.Count > 0)
        {
            var listedLot = await TryFindInOwnerLotsAsync(siteId, tenantIds, bearer, ct);
            if (listedLot is not null)
            {
                return new OwnerSiteScopeResult(scope.UserId, listedLot.TenantId, siteId);
            }
        }

        var existsElsewhere = await ProbeSiteExistsInFacilityAsync(siteId, bearer, ct);
        if (existsElsewhere)
        {
            throw new PaymentDomainException("FORBIDDEN_SITE_SCOPE", "You do not own this parking lot.");
        }

        throw new PaymentDomainException("SITE_NOT_FOUND", "Parking lot not found.", nameof(siteId));
    }

    private async Task<UserOwnerScopeHttpDto> GetOwnerScopeAsync(string bearer, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/owner-scope?permission=PARKING_MANAGE");
            request.Headers.TryAddWithoutValidation("Authorization", bearer);

            using var response = await userClient.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new PaymentDomainException("UNAUTHENTICATED", "Session is invalid or revoked.");
            }

            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new PaymentDomainException("FORBIDDEN_SITE_SCOPE", "Owner parking management permission is required.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new PaymentDomainException("IDENTITY_UNAVAILABLE", "Owner authority service is unavailable.");
            }

            var dto = await response.Content.ReadFromJsonAsync<UserOwnerScopeHttpDto>(JsonOptions, ct);
            if (dto is null || dto.UserId == Guid.Empty)
            {
                throw new PaymentDomainException("IDENTITY_UNAVAILABLE", "Owner authority service returned an invalid payload.");
            }

            return dto;
        }
        catch (PaymentDomainException)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is HttpRequestException or JsonException ||
            ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new PaymentDomainException("IDENTITY_UNAVAILABLE", "Owner authority service is unavailable.");
        }
    }

    private async Task<FacilityValidatedSiteHttpDto?> TryValidateSiteWithFacilityAsync(
        Guid siteId,
        IReadOnlyList<Guid> tenantIds,
        CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "internal/sites/validate");
            request.Headers.TryAddWithoutValidation("X-Service-Key", internalServiceKey);
            request.Content = JsonContent.Create(new FacilityValidateSitesHttpRequest([siteId], tenantIds));

            using var response = await facilityClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var sites = await response.Content.ReadFromJsonAsync<FacilityValidatedSiteHttpDto[]>(JsonOptions, ct);
            if (sites is null)
            {
                throw new PaymentDomainException("FACILITY_UNAVAILABLE", "Facility service returned an invalid validation response.");
            }

            return sites.FirstOrDefault(s => s.Id == siteId);
        }
        catch (PaymentDomainException)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is HttpRequestException or JsonException ||
            ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new PaymentDomainException("FACILITY_UNAVAILABLE", "Facility service is temporarily unavailable.");
        }
    }

    private async Task<FacilityLotItemHttpDto?> TryFindInOwnerLotsAsync(
        Guid siteId,
        IReadOnlyList<Guid> tenantIds,
        string bearer,
        CancellationToken ct)
    {
        foreach (var tenantId in tenantIds)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"api/parking-lots?tenantId={tenantId:D}");
                request.Headers.TryAddWithoutValidation("Authorization", bearer);

                using var response = await facilityClient.SendAsync(request, ct);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new PaymentDomainException("UNAUTHENTICATED", "Session is invalid or revoked.");
                }

                if (!response.IsSuccessStatusCode)
                {
                    continue;
                }

                var envelope = await response.Content.ReadFromJsonAsync<FacilityLotsEnvelopeHttpDto>(JsonOptions, ct);
                if (envelope is not { Success: true, Data: not null })
                {
                    throw new PaymentDomainException("FACILITY_UNAVAILABLE", "Facility service returned an invalid lot listing response.");
                }

                var matched = envelope.Data.FirstOrDefault(lot => lot.Id == siteId);
                if (matched is not null)
                {
                    return matched;
                }
            }
            catch (PaymentDomainException)
            {
                throw;
            }
            catch (Exception ex) when (
                ex is HttpRequestException or JsonException ||
                ex is OperationCanceledException && !ct.IsCancellationRequested)
            {
                throw new PaymentDomainException("FACILITY_UNAVAILABLE", "Facility service is temporarily unavailable.");
            }
        }

        return null;
    }

    private async Task<bool> ProbeSiteExistsInFacilityAsync(Guid siteId, string bearer, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/parking-lots/{siteId:D}/operational-layout");
            request.Headers.TryAddWithoutValidation("Authorization", bearer);

            using var response = await facilityClient.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var envelope = await response.Content.ReadFromJsonAsync<FacilityOperationalLayoutEnvelopeHttpDto>(JsonOptions, ct);
                if (envelope is not { Success: true })
                {
                    throw new PaymentDomainException("FACILITY_UNAVAILABLE", "Facility service returned an invalid layout response.");
                }

                return true;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new PaymentDomainException("UNAUTHENTICATED", "Session is invalid or revoked.");
            }

            throw new PaymentDomainException("FACILITY_UNAVAILABLE", "Facility service is temporarily unavailable.");
        }
        catch (PaymentDomainException)
        {
            throw;
        }
        catch (Exception ex) when (
            ex is HttpRequestException or JsonException ||
            ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new PaymentDomainException("FACILITY_UNAVAILABLE", "Facility service is temporarily unavailable.");
        }
    }
}
