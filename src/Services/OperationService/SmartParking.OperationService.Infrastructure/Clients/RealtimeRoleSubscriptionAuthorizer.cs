using System.Net;
using System.Text.Json;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;

namespace SmartParking.OperationService.Infrastructure.Clients;

public sealed class RealtimeRoleSubscriptionAuthorizer(
    IHttpClientFactory httpClientFactory) : IRealtimeRoleSubscriptionAuthorizer
{
    public async Task AuthorizeOwnerSitesAsync(IReadOnlyCollection<Guid> siteIds, string bearerToken, CancellationToken ct)
    {
        if (siteIds.Count == 0 || siteIds.Any(id => id == Guid.Empty))
            throw new ReservationGateException(400, "INVALID_SITE_ID", "Valid parking site identifiers are required.");

        var bearer = NormalizeBearer(bearerToken);
        var authorizedTenantIds = await FetchOwnerTenantIdsAsync(bearer, ct);
        if (authorizedTenantIds.Count == 0)
            throw new ReservationGateException(403, "OWNER_SITE_UNAUTHORIZED", "You do not own any authorized parking sites.");

        var ownedSiteIds = await FetchFacilityOwnedSiteIdsAsync(bearer, authorizedTenantIds, ct);
        foreach (var siteId in siteIds)
        {
            if (!ownedSiteIds.Contains(siteId))
                throw new ReservationGateException(403, "OWNER_SITE_UNAUTHORIZED", "This parking site does not belong to the current owner.");
        }
    }

    public async Task AuthorizeDriverSitesAsync(IReadOnlyCollection<Guid> siteIds, string bearerToken, CancellationToken ct)
    {
        if (siteIds.Count == 0 || siteIds.Any(id => id == Guid.Empty))
            throw new ReservationGateException(400, "INVALID_SITE_ID", "Valid parking site identifiers are required.");

        var bearer = NormalizeBearer(bearerToken);
        var facilityClient = httpClientFactory.CreateClient("RealtimeFacilityAuthority");

        foreach (var siteId in siteIds)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"api/parking-lots/{siteId}/operational-layout");
                request.Headers.TryAddWithoutValidation("Authorization", bearer);
                using var response = await facilityClient.SendAsync(request, ct);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                    throw new ReservationGateException(401, "UNAUTHENTICATED", "Session is invalid or revoked.");
                if (response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.NotFound)
                    throw new ReservationGateException(403, "SITE_NOT_DISCOVERABLE", "This parking site is not publicly discoverable.");
                if (!response.IsSuccessStatusCode)
                    throw new ReservationGateException(503, "FACILITY_UNAVAILABLE", "Parking facility authority is unavailable.");
            }
            catch (ReservationGateException)
            {
                throw;
            }
            catch (Exception error) when (error is HttpRequestException || error is OperationCanceledException && !ct.IsCancellationRequested)
            {
                throw new ReservationGateException(503, "FACILITY_UNAVAILABLE", "Parking facility authority is unavailable.");
            }
        }
    }

    private async Task<HashSet<Guid>> FetchOwnerTenantIdsAsync(string bearer, CancellationToken ct)
    {
        var userClient = httpClientFactory.CreateClient("RealtimeUserAuthority");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/owner-scope?permission=PARKING_MANAGE");
            request.Headers.TryAddWithoutValidation("Authorization", bearer);
            using var response = await userClient.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new ReservationGateException(401, "UNAUTHENTICATED", "Session is invalid or revoked.");
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new ReservationGateException(403, "OWNER_SITE_UNAUTHORIZED", "You do not have parking management permissions.");
            if (!response.IsSuccessStatusCode)
                throw new ReservationGateException(503, "IDENTITY_UNAVAILABLE", "Owner authority service is unavailable.");

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;
            if (!TryGetGuid(root, "userId", out var userId) || userId == Guid.Empty)
                throw new ReservationGateException(403, "OWNER_SITE_UNAUTHORIZED", "Owner identity could not be verified.");

            return ReadGuidArray(root, "tenantIds");
        }
        catch (ReservationGateException)
        {
            throw;
        }
        catch (Exception error) when (error is HttpRequestException or JsonException || error is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new ReservationGateException(503, "IDENTITY_UNAVAILABLE", "Owner authority service is unavailable.");
        }
    }

    private async Task<HashSet<Guid>> FetchFacilityOwnedSiteIdsAsync(string bearer, HashSet<Guid> authorizedTenantIds, CancellationToken ct)
    {
        var facilityClient = httpClientFactory.CreateClient("RealtimeFacilityAuthority");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/parking-lots");
            request.Headers.TryAddWithoutValidation("Authorization", bearer);
            using var response = await facilityClient.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new ReservationGateException(401, "UNAUTHENTICATED", "Session is invalid or revoked.");
            if (response.StatusCode == HttpStatusCode.Forbidden)
                throw new ReservationGateException(403, "OWNER_SITE_UNAUTHORIZED", "You do not have parking management permissions.");
            if (!response.IsSuccessStatusCode)
                throw new ReservationGateException(503, "FACILITY_UNAVAILABLE", "Parking facility authority is unavailable.");

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!TryGetPropertyIgnoreCase(doc.RootElement, "data", out var dataElement) ||
                dataElement.ValueKind != JsonValueKind.Array)
            {
                throw new ReservationGateException(503, "FACILITY_UNAVAILABLE", "Parking facility authority returned an invalid site list.");
            }

            var siteIds = new HashSet<Guid>();
            foreach (var item in dataElement.EnumerateArray())
            {
                if (!TryGetGuid(item, "id", out var siteId) || siteId == Guid.Empty) continue;
                if (!TryGetGuid(item, "tenantId", out var itemTenantId) ||
                    itemTenantId == Guid.Empty ||
                    !authorizedTenantIds.Contains(itemTenantId))
                {
                    continue;
                }
                siteIds.Add(siteId);
            }
            return siteIds;
        }
        catch (ReservationGateException)
        {
            throw;
        }
        catch (Exception error) when (error is HttpRequestException or JsonException || error is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new ReservationGateException(503, "FACILITY_UNAVAILABLE", "Parking facility authority is unavailable.");
        }
    }

    private static HashSet<Guid> ReadGuidArray(JsonElement root, string propertyName)
    {
        var result = new HashSet<Guid>();
        if (!TryGetPropertyIgnoreCase(root, propertyName, out var prop) || prop.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && Guid.TryParse(item.GetString(), out var guid) && guid != Guid.Empty)
                result.Add(guid);
        }
        return result;
    }

    private static bool TryGetGuid(JsonElement element, string propertyName, out Guid value)
    {
        value = Guid.Empty;
        if (!TryGetPropertyIgnoreCase(element, propertyName, out var prop) || prop.ValueKind != JsonValueKind.String)
            return false;
        return Guid.TryParse(prop.GetString(), out value);
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = prop.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    private static string NormalizeBearer(string bearerToken)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
            throw new ReservationGateException(401, "UNAUTHENTICATED", "Authentication token is required.");
        return bearerToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? bearerToken
            : $"Bearer {bearerToken}";
    }
}
