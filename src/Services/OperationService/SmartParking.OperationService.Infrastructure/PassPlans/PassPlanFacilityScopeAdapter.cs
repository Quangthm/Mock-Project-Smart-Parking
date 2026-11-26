using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.PassPlans.DTOs;
using SmartParking.OperationService.Application.PassPlans.Ports;
using SmartParking.OperationService.Domain.PassPlans;
using SmartParking.OperationService.Domain.Policies;
using SmartParking.OperationService.Infrastructure.PassPlans.Models;

namespace SmartParking.OperationService.Infrastructure.PassPlans;

public sealed class PassPlanFacilityScopeAdapter(
    HttpClient facilityClient,
    IOwnerIdentityClient identityClient,
    IPassPlanRepository passPlans,
    IHttpContextAccessor httpContextAccessor,
    IConfiguration configuration) : IPassPlanFacilityScopePort
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<OwnerSiteScopeResult> ValidateOwnerSiteAsync(Guid siteId, CancellationToken ct)
    {
        if (siteId == Guid.Empty)
        {
            throw new PassPlanException(404, "SITE_NOT_FOUND", "Parking lot not found.");
        }

        OwnerIdentityScope scope;
        try
        {
            scope = await identityClient.GetOwnerScopeAsync("PARKING_MANAGE", ct);
        }
        catch (ReservationGateException ex) when (ex.StatusCode == 401)
        {
            throw new PassPlanException(401, "UNAUTHENTICATED", ex.Message);
        }
        catch (ReservationGateException ex)
        {
            throw new PassPlanException(ex.StatusCode, ex.Code, ex.Message);
        }
        catch (CrossLotAccessException ex)
        {
            throw new PassPlanException(403, "FORBIDDEN_SITE_SCOPE", ex.Message);
        }

        var localTenantId = await passPlans.GetLocalSiteTenantIdAsync(siteId, ct);
        if (localTenantId.HasValue)
        {
            if (!scope.TenantIds.Contains(localTenantId.Value))
            {
                throw new PassPlanException(403, "FORBIDDEN_SITE_SCOPE", "You do not own this parking lot.");
            }
            return new OwnerSiteScopeResult(scope.UserId, localTenantId.Value, siteId);
        }

        var bearer = GetBearerToken();
        var validated = await TryValidateSiteWithFacilityAsync(siteId, scope.TenantIds, ct);
        if (validated is not null)
        {
            return new OwnerSiteScopeResult(scope.UserId, validated.TenantId, siteId);
        }

        var listedLot = await TryFindInOwnerLotsAsync(siteId, scope.TenantIds, bearer, ct);
        if (listedLot is not null)
        {
            return new OwnerSiteScopeResult(scope.UserId, listedLot.TenantId, siteId);
        }

        var existsElsewhere = await ProbeSiteExistsInFacilityAsync(siteId, bearer, ct);
        if (existsElsewhere)
        {
            throw new PassPlanException(403, "FORBIDDEN_SITE_SCOPE", "You do not own this parking lot.");
        }

        throw new PassPlanException(404, "SITE_NOT_FOUND", "Parking lot not found.");
    }

    public async Task<OwnerSiteScopeResult> ValidateOwnerDraftScopeAsync(
        Guid siteId,
        Guid? zoneId,
        IReadOnlyList<PassVehicleType> vehicleTypes,
        IReadOnlyList<Guid> excludedZoneIds,
        CancellationToken ct)
    {
        var ownerSite = await ValidateOwnerSiteAsync(siteId, ct);
        var bearer = GetBearerToken();

        var structure = await ReadFacilityStructureAsync(siteId, ownerSite.TenantId, bearer, ct);
        var layout = await TryReadOperationalLayoutAsync(siteId, bearer, ct);

        var zoneUnits = (structure.Units ?? [])
            .Where(u => string.Equals(u.Type, "ZONE", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var validZoneIds = zoneUnits.Select(u => u.Id).ToHashSet();
        if (layout?.Metrics?.ZoneCapacities is { Count: > 0 } layoutZones)
        {
            foreach (var z in layoutZones)
            {
                validZoneIds.Add(z.ZoneId);
            }
        }

        if (zoneId.HasValue && !validZoneIds.Contains(zoneId.Value))
        {
            throw new PassPlanException(
                422,
                "UNSUPPORTED_LOT_SCOPE",
                $"Zone '{zoneId.Value}' does not belong to parking lot '{siteId}'.",
                "zoneId");
        }

        foreach (var excluded in excludedZoneIds)
        {
            if (!validZoneIds.Contains(excluded))
            {
                throw new PassPlanException(
                    422,
                    "UNSUPPORTED_LOT_SCOPE",
                    $"Excluded zone '{excluded}' does not belong to parking lot '{siteId}'.",
                    "ordinaryCoverageRules.excludedZoneIds");
            }
        }

        var supportedVehicles = new HashSet<PassVehicleType>();
        foreach (var slot in structure.Slots ?? [])
        {
            if (TryParseVehicleType(slot.VehicleType, out var primary))
            {
                supportedVehicles.Add(primary);
            }
            if (slot.VehicleTypes is not null)
            {
                foreach (var compat in slot.VehicleTypes)
                {
                    if (TryParseVehicleType(compat, out var compatParsed))
                    {
                        supportedVehicles.Add(compatParsed);
                    }
                }
            }
        }

        foreach (var view in structure.CapacityViews ?? [])
        {
            if (view.Total > 0 && TryParseVehicleType(view.VehicleType, out var viewVehicle))
            {
                supportedVehicles.Add(viewVehicle);
            }
        }

        var motorcycleMaxCapacity = layout?.Metrics?.MotorcycleCapacity?.MaxCapacity ?? 0;
        if (motorcycleMaxCapacity > 0)
        {
            supportedVehicles.Add(PassVehicleType.MOTORCYCLE);
        }

        var localSupported = await passPlans.GetLocalSupportedVehicleTypesAsync(siteId, ct);
        foreach (var localVehicle in localSupported)
        {
            supportedVehicles.Add(localVehicle);
        }

        if (supportedVehicles.Count == 0)
        {
            throw new PassPlanException(
                422,
                "UNSUPPORTED_LOT_SCOPE",
                $"Parking lot '{siteId}' has no configured slots or capacity for pass vehicle types.",
                "vehicleTypes");
        }

        foreach (var requested in vehicleTypes)
        {
            if (!supportedVehicles.Contains(requested))
            {
                throw new PassPlanException(
                    422,
                    "UNSUPPORTED_LOT_SCOPE",
                    $"Vehicle type '{requested}' is not supported at parking lot '{siteId}'.",
                    "vehicleTypes");
            }
        }

        if (zoneId.HasValue)
        {
            var zoneUnit = zoneUnits.FirstOrDefault(u => u.Id == zoneId.Value);
            var unitById = (structure.Units ?? []).ToDictionary(u => u.Id);
            var zoneSlots = (structure.Slots ?? [])
                .Where(s => IsSlotInZoneSubtree(s.UnitId, zoneId.Value, zoneUnit?.Path, unitById))
                .ToArray();

            if (zoneSlots.Length > 0)
            {
                var zoneSupported = new HashSet<PassVehicleType>();
                foreach (var slot in zoneSlots)
                {
                    if (TryParseVehicleType(slot.VehicleType, out var primary))
                        zoneSupported.Add(primary);
                    if (slot.VehicleTypes is not null)
                    {
                        foreach (var compat in slot.VehicleTypes)
                        {
                            if (TryParseVehicleType(compat, out var compatParsed))
                                zoneSupported.Add(compatParsed);
                        }
                    }
                }

                foreach (var requested in vehicleTypes)
                {
                    if (requested == PassVehicleType.MOTORCYCLE && motorcycleMaxCapacity > 0)
                        continue;

                    if (!zoneSupported.Contains(requested))
                    {
                        throw new PassPlanException(
                            422,
                            "UNSUPPORTED_LOT_SCOPE",
                            $"Zone '{zoneId.Value}' does not support vehicle type '{requested}'.",
                            "vehicleTypes");
                    }
                }
            }
        }

        return ownerSite;
    }

    public async Task<bool> SiteExistsAsync(Guid siteId, CancellationToken ct)
    {
        if (siteId == Guid.Empty) return false;
        if (await passPlans.HasLocalSiteEvidenceAsync(siteId, ct))
        {
            return true;
        }

        var bearer = GetBearerToken();
        return await ProbeSiteExistsInFacilityAsync(siteId, bearer, ct);
    }

    private string? GetBearerToken()
    {
        var bearer = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        return string.IsNullOrWhiteSpace(bearer) ? null : bearer;
    }

    private async Task<FacilityValidatedSiteDto?> TryValidateSiteWithFacilityAsync(
        Guid siteId,
        IReadOnlyList<Guid> tenantIds,
        CancellationToken ct)
    {
        var serviceKey = configuration["Services:Key"];
        if (string.IsNullOrWhiteSpace(serviceKey) || tenantIds.Count == 0)
        {
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "internal/sites/validate");
            request.Headers.TryAddWithoutValidation("X-Service-Key", serviceKey);
            request.Content = JsonContent.Create(new FacilityValidateSiteRequestDto([siteId], tenantIds.ToArray()));

            using var response = await facilityClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var sites = await response.Content.ReadFromJsonAsync<FacilityValidatedSiteDto[]>(JsonOptions, ct);
            return sites?.FirstOrDefault(s => s.Id == siteId);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException ||
                                   ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    private async Task<FacilityLotSummaryDto?> TryFindInOwnerLotsAsync(
        Guid siteId,
        IReadOnlyList<Guid> tenantIds,
        string? bearer,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(bearer) || tenantIds.Count == 0)
        {
            return null;
        }

        foreach (var tenantId in tenantIds)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"api/parking-lots?tenantId={tenantId:D}");
                request.Headers.TryAddWithoutValidation("Authorization", bearer);
                using var response = await facilityClient.SendAsync(request, ct);
                if (!response.IsSuccessStatusCode) continue;

                var envelope = await response.Content.ReadFromJsonAsync<FacilityLotsEnvelopeDto>(JsonOptions, ct);
                var matched = envelope?.Data?.FirstOrDefault(lot => lot.Id == siteId);
                if (matched is not null)
                {
                    return matched;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException ||
                                       ex is OperationCanceledException && !ct.IsCancellationRequested)
            {
                throw new PassPlanException(503, "FACILITY_UNAVAILABLE", "Facility service is temporarily unavailable.");
            }
        }

        return null;
    }

    private async Task<bool> ProbeSiteExistsInFacilityAsync(Guid siteId, string? bearer, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/parking-lots/{siteId:D}/operational-layout");
            if (!string.IsNullOrWhiteSpace(bearer))
            {
                request.Headers.TryAddWithoutValidation("Authorization", bearer);
            }

            using var response = await facilityClient.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var envelope = await response.Content.ReadFromJsonAsync<FacilityOperationalLayoutEnvelopeDto>(JsonOptions, ct);
                if (envelope is not { Success: true, Data: not null })
                {
                    throw new PassPlanException(503, "FACILITY_UNAVAILABLE", "Facility service returned an invalid response.");
                }
                return true;
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new PassPlanException(401, "UNAUTHENTICATED", "Session is invalid or revoked.");
            }

            throw new PassPlanException(503, "FACILITY_UNAVAILABLE", "Facility service is temporarily unavailable.");
        }
        catch (PassPlanException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException ||
                                   ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new PassPlanException(503, "FACILITY_UNAVAILABLE", "Facility service is temporarily unavailable.");
        }
    }

    private async Task<FacilityStructureDataDto> ReadFacilityStructureAsync(
        Guid siteId,
        Guid tenantId,
        string? bearer,
        CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"api/parking-lots/{siteId:D}/structure?tenantId={tenantId:D}");
            if (!string.IsNullOrWhiteSpace(bearer))
            {
                request.Headers.TryAddWithoutValidation("Authorization", bearer);
            }

            using var response = await facilityClient.SendAsync(request, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new PassPlanException(401, "UNAUTHENTICATED", "Session is invalid or revoked.");
            }
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                throw new PassPlanException(403, "FORBIDDEN_SITE_SCOPE", "You do not own this parking lot.");
            }
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new PassPlanException(404, "SITE_NOT_FOUND", "Parking lot not found.");
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new PassPlanException(503, "FACILITY_UNAVAILABLE", "Facility service is temporarily unavailable.");
            }

            var envelope = await response.Content.ReadFromJsonAsync<FacilityStructureEnvelopeDto>(JsonOptions, ct);
            if (envelope is not { Success: true, Data: not null })
            {
                throw new PassPlanException(503, "FACILITY_UNAVAILABLE", "Facility service returned an invalid structure response.");
            }

            return envelope.Data;
        }
        catch (PassPlanException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException ||
                                   ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            throw new PassPlanException(503, "FACILITY_UNAVAILABLE", "Facility service is temporarily unavailable.");
        }
    }

    private async Task<FacilityOperationalLayoutDataDto?> TryReadOperationalLayoutAsync(
        Guid siteId,
        string? bearer,
        CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"api/parking-lots/{siteId:D}/operational-layout");
            if (!string.IsNullOrWhiteSpace(bearer))
            {
                request.Headers.TryAddWithoutValidation("Authorization", bearer);
            }

            using var response = await facilityClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var envelope = await response.Content.ReadFromJsonAsync<FacilityOperationalLayoutEnvelopeDto>(JsonOptions, ct);
            return envelope is { Success: true } ? envelope.Data : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException ||
                                   ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    private static bool TryParseVehicleType(string? raw, out PassVehicleType parsed) =>
        Enum.TryParse(raw, ignoreCase: true, out parsed) && Enum.IsDefined(parsed);

    private static bool IsSlotInZoneSubtree(
        Guid slotUnitId,
        Guid zoneId,
        string? zonePath,
        IReadOnlyDictionary<Guid, FacilityUnitItemDto> unitsById)
    {
        if (slotUnitId == zoneId) return true;
        if (unitsById.TryGetValue(slotUnitId, out var slotUnit))
        {
            if (!string.IsNullOrWhiteSpace(zonePath) && !string.IsNullOrWhiteSpace(slotUnit.Path))
            {
                return slotUnit.Path.StartsWith(zonePath, StringComparison.OrdinalIgnoreCase);
            }

            var current = slotUnit.ParentId;
            for (var depth = 0; depth < 32 && current.HasValue; depth++)
            {
                if (current.Value == zoneId) return true;
                current = unitsById.TryGetValue(current.Value, out var parent) ? parent.ParentId : null;
            }
        }
        return false;
    }
}
