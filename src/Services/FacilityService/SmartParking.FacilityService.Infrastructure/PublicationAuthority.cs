using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Infrastructure;

public sealed class PublicationAuthority(HttpClient client, IHttpContextAccessor current) : IPublicationAuthority
{
    public async Task<ApprovedSite> RequireApprovedSite(Guid siteId, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "api/auth/owner-scope/publication-evidence")
            { Content = JsonContent.Create(new { siteId }) };
        return await Send<ApprovedSite>(message, ct);
    }
    public async Task<Guid[]> ManagedTenants(CancellationToken ct)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, "api/auth/owner-scope");
        var scope = await Send<IdentityScope>(message, ct);
        if (scope.UserId == Guid.Empty || scope.TenantIds == null) throw Unavailable();
        return scope.TenantIds;
    }
    private async Task<T> Send<T>(HttpRequestMessage message, CancellationToken ct)
    {
        if (!AuthenticationHeaderValue.TryParse(current.HttpContext?.Request.Headers.Authorization.ToString(), out var auth) ||
            !string.Equals(auth.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(auth.Parameter))
            throw new StructureException("UNAUTHENTICATED", "Sign in as an Owner.");
        if (client.BaseAddress == null) throw Unavailable();
        message.Headers.Authorization = auth;
        try
        {
            using var response = await client.SendAsync(message, ct);
            if (response.StatusCode == HttpStatusCode.Unauthorized) throw new StructureException("UNAUTHENTICATED", "Sign in as an Owner.");
            if (response.StatusCode == HttpStatusCode.Forbidden) throw new StructureException("FORBIDDEN", "Owner permission is required.");
            if (response.StatusCode == HttpStatusCode.NotFound) throw new StructureException("SITE_NOT_FOUND", "Approved site was not found.");
            if (response.StatusCode != HttpStatusCode.OK) throw Unavailable();
            return await response.Content.ReadFromJsonAsync<T>(ct) ?? throw Unavailable();
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or NotSupportedException ||
            e is OperationCanceledException && !ct.IsCancellationRequested) { throw Unavailable(); }
    }
    private static StructureException Unavailable() => new("IDENTITY_UNAVAILABLE", "Publication authority is unavailable.");
}
