using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Infrastructure.Authentication;

namespace SmartParking.OperationService.API.Hubs;

public sealed class ParkingLotLayoutHub(
    IOperatorIdentityClient identity,
    IRealtimeRoleSubscriptionAuthorizer? roleAuthorizer = null,
    IConfiguration? configuration = null) : Hub
{
    public const int MaxOwnerSites = 20;
    public const int MaxDriverVisibleSites = 10;

    private const string BearerContextKey = "realtime_bearer";
    private const string RoleContextKey = "realtime_role";
    private const string OwnerSitesContextKey = "owner_sites";
    private const string DriverSitesContextKey = "driver_sites";

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        EnsureAuthenticatedAndKeysAvailable(http);

        var token = ResolveToken(http);
        if (string.IsNullOrWhiteSpace(token))
            throw new HubException("UNAUTHENTICATED");

        Context.Items[BearerContextKey] = token;
        var role = Context.User?.FindFirst("role")?.Value?.Trim();

        if (IsSharedMultiSitePath(http))
        {
            EnsureSharedHubEnabled();
            if (string.IsNullOrWhiteSpace(role) ||
                (!string.Equals(role, "operator", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(role, "owner", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(role, "driver", StringComparison.OrdinalIgnoreCase)))
            {
                throw new HubException("FORBIDDEN");
            }

            Context.Items[RoleContextKey] = role.ToLowerInvariant();
            await base.OnConnectedAsync();
            return;
        }

        if (!TryResolveSiteId(http, out var siteId))
            throw new HubException("INVALID_SITE_ID");

        if (!string.Equals(role, "operator", StringComparison.OrdinalIgnoreCase))
            throw new HubException("OPERATOR_LOT_UNASSIGNED");

        try
        {
            await identity.AuthorizeSiteAsync(siteId, token, Context.ConnectionAborted);
        }
        catch (ReservationGateException ex)
        {
            throw ToHubException(ex, "OPERATOR_LOT_UNASSIGNED");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"site:{siteId}:operators", Context.ConnectionAborted);
        await Clients.Caller.SendAsync("SnapshotRequired", siteId, Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public async Task SubscribeOwnerSites(Guid[]? siteIds, Guid? selectedSiteId = null)
    {
        EnsureSharedHubEnabled();
        EnsureAuthenticatedAndKeysAvailable(Context.GetHttpContext());
        if (!HasRole("owner"))
            throw new HubException("OWNER_SITE_UNAUTHORIZED");

        var authorizer = roleAuthorizer ?? throw new HubException("IDENTITY_UNAVAILABLE");
        var requested = CollectValidSites(siteIds, selectedSiteId);
        var current = GetOrCreateSiteSet(OwnerSitesContextKey);

        var union = new HashSet<Guid>(current);
        union.UnionWith(requested);
        if (union.Count > MaxOwnerSites)
            throw new HubException("OWNER_SITE_LIMIT_EXCEEDED");

        var toAdd = requested.Where(id => !current.Contains(id)).ToArray();
        if (toAdd.Length > 0)
        {
            var token = RequireToken();
            try
            {
                await authorizer.AuthorizeOwnerSitesAsync(toAdd, token, Context.ConnectionAborted);
            }
            catch (ReservationGateException ex)
            {
                throw ToHubException(ex, "OWNER_SITE_UNAUTHORIZED");
            }

            foreach (var siteId in toAdd)
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"site:{siteId}:owners", Context.ConnectionAborted);
                current.Add(siteId);
                await Clients.Caller.SendAsync("SnapshotRequired", siteId, Context.ConnectionAborted);
            }
        }
    }

    public async Task UnsubscribeOwnerSites(Guid[]? siteIds)
    {
        EnsureSharedHubEnabled();
        EnsureAuthenticatedAndKeysAvailable(Context.GetHttpContext());
        if (!HasRole("owner"))
            throw new HubException("OWNER_SITE_UNAUTHORIZED");

        if (siteIds is null || siteIds.Length == 0) return;
        var current = GetOrCreateSiteSet(OwnerSitesContextKey);
        foreach (var siteId in siteIds)
        {
            if (siteId == Guid.Empty) continue;
            if (current.Remove(siteId))
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"site:{siteId}:owners", Context.ConnectionAborted);
        }
    }

    public async Task SubscribeDriverSites(Guid[]? visibleSiteIds, Guid? selectedSiteId = null)
    {
        EnsureSharedHubEnabled();
        EnsureAuthenticatedAndKeysAvailable(Context.GetHttpContext());
        if (!HasRole("driver"))
            throw new HubException("FORBIDDEN");

        var authorizer = roleAuthorizer ?? throw new HubException("FACILITY_UNAVAILABLE");
        var visible = CollectValidSites(visibleSiteIds, null);
        if (visible.Count > MaxDriverVisibleSites)
            throw new HubException("DRIVER_SITE_LIMIT_EXCEEDED");

        if (selectedSiteId == Guid.Empty)
            throw new HubException("INVALID_SITE_ID");

        var targetSet = new HashSet<Guid>(visible);
        if (selectedSiteId is { } sel)
            targetSet.Add(sel);

        var current = GetOrCreateSiteSet(DriverSitesContextKey);
        var toAdd = targetSet.Where(id => !current.Contains(id)).ToArray();
        if (toAdd.Length > 0)
        {
            var token = RequireToken();
            try
            {
                await authorizer.AuthorizeDriverSitesAsync(toAdd, token, Context.ConnectionAborted);
            }
            catch (ReservationGateException ex)
            {
                throw ToHubException(ex, "SITE_NOT_DISCOVERABLE");
            }
        }

        var toRemove = current.Where(id => !targetSet.Contains(id)).ToArray();
        foreach (var oldSiteId in toRemove)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"site:{oldSiteId}:drivers", Context.ConnectionAborted);
            current.Remove(oldSiteId);
        }

        foreach (var newSiteId in toAdd)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"site:{newSiteId}:drivers", Context.ConnectionAborted);
            current.Add(newSiteId);
            await Clients.Caller.SendAsync("SnapshotRequired", newSiteId, Context.ConnectionAborted);
        }
    }

    private void EnsureSharedHubEnabled()
    {
        if (configuration?.GetValue<bool>("Realtime:SharedMultiSiteHubEnabled") != true)
            throw new HubException("REALTIME_SHARED_HUB_DISABLED");
    }

    private void EnsureAuthenticatedAndKeysAvailable(HttpContext? http)
    {
        if (http?.RequestServices?.GetService<JwksConfigurationManager>() is null ||
            ConsumerAuthenticationRegistration.KeysUnavailable(http))
        {
            throw new HubException("AUTHENTICATION_KEYS_UNAVAILABLE");
        }
        if (Context.User?.Identity?.IsAuthenticated != true)
            throw new HubException("UNAUTHENTICATED");
    }

    private bool HasRole(string expectedRole)
    {
        var role = Context.Items[RoleContextKey] as string ?? Context.User?.FindFirst("role")?.Value;
        return string.Equals(role, expectedRole, StringComparison.OrdinalIgnoreCase);
    }

    private string RequireToken()
    {
        if (Context.Items[BearerContextKey] is string stored && !string.IsNullOrWhiteSpace(stored))
            return stored;
        var resolved = ResolveToken(Context.GetHttpContext());
        if (string.IsNullOrWhiteSpace(resolved))
            throw new HubException("UNAUTHENTICATED");
        Context.Items[BearerContextKey] = resolved;
        return resolved;
    }

    private HashSet<Guid> GetOrCreateSiteSet(string key)
    {
        if (Context.Items[key] is HashSet<Guid> existing)
            return existing;
        var created = new HashSet<Guid>();
        Context.Items[key] = created;
        return created;
    }

    private static HashSet<Guid> CollectValidSites(Guid[]? siteIds, Guid? selectedSiteId)
    {
        var sites = new HashSet<Guid>();
        if (siteIds is not null)
        {
            foreach (var siteId in siteIds)
            {
                if (siteId == Guid.Empty)
                    throw new HubException("INVALID_SITE_ID");
                sites.Add(siteId);
            }
        }
        if (selectedSiteId is { } selected)
        {
            if (selected == Guid.Empty)
                throw new HubException("INVALID_SITE_ID");
            sites.Add(selected);
        }
        return sites;
    }

    private static HubException ToHubException(ReservationGateException ex, string forbiddenFallback)
    {
        if (ex.StatusCode == 401 || string.Equals(ex.Code, "UNAUTHENTICATED", StringComparison.Ordinal))
            return new HubException("UNAUTHENTICATED");
        if (ex.StatusCode == 403)
            return new HubException(string.IsNullOrWhiteSpace(ex.Code) ? forbiddenFallback : ex.Code);
        if (ex.StatusCode == 400)
            return new HubException(string.IsNullOrWhiteSpace(ex.Code) ? "INVALID_SITE_ID" : ex.Code);
        if (ex.StatusCode == 503)
            return new HubException(string.IsNullOrWhiteSpace(ex.Code) ? "IDENTITY_UNAVAILABLE" : ex.Code);
        return new HubException(forbiddenFallback);
    }

    private static string? ResolveToken(HttpContext? http)
    {
        var token = http?.Request.Query["access_token"].ToString();
        if (string.IsNullOrWhiteSpace(token))
        {
            var authHeader = http?.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrWhiteSpace(authHeader)) token = authHeader;
        }
        return token;
    }

    internal static bool IsSharedMultiSitePath(HttpContext? http)
    {
        var path = http?.Request.Path.Value;
        if (string.IsNullOrWhiteSpace(path)) return false;
        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length == 3 &&
               string.Equals(segments[0], "ws", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(segments[1], "parking-lots", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(segments[2], "layout", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool TryResolveSiteId(HttpContext? http, out Guid siteId)
    {
        siteId = Guid.Empty;
        var raw = http?.Request.RouteValues["siteId"]?.ToString();
        if (string.IsNullOrWhiteSpace(raw))
            raw = ExtractSiteSegmentFromPath(http?.Request.Path.Value);

        return Guid.TryParse(raw, out siteId) && siteId != Guid.Empty;
    }

    private static string? ExtractSiteSegmentFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var segments = path.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 4 &&
            string.Equals(segments[0], "ws", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(segments[1], "parking-lots", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(segments[3], "layout", StringComparison.OrdinalIgnoreCase))
        {
            return Uri.UnescapeDataString(segments[2]);
        }
        return null;
    }
}
