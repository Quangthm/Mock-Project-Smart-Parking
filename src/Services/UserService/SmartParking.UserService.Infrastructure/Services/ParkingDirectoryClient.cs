using System.Net.Http.Json;
using System.Text.Json;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;

namespace UserService.Infrastructure.Services;

public sealed class ParkingDirectoryClient(HttpClient client) : IParkingDirectory
{
    private static AuthException Unavailable() => new("PARKING_UNAVAILABLE", "Parking service is unavailable. Please retry.", 503);
    public async Task ProvisionTenantAsync(Guid tenantId, string name, string email, string phone, CancellationToken ct)
    {
        try
        {
            using var response = await client.PutAsJsonAsync($"internal/tenants/{tenantId}", new { name, email, phone }, ct);
            if (!response.IsSuccessStatusCode) throw Unavailable();
        }
        catch (HttpRequestException) { throw Unavailable(); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Unavailable(); }
        catch (JsonException) { throw Unavailable(); }
    }
    public async Task<IReadOnlyList<DirectorySite>> ActiveSitesAsync(Guid[] siteIds, Guid[] tenantIds, CancellationToken ct)
    {
        try
        {
            using var response = await client.PostAsJsonAsync("internal/sites/validate", new { siteIds, tenantIds }, ct);
            if (!response.IsSuccessStatusCode) throw Unavailable();
            return await response.Content.ReadFromJsonAsync<DirectorySite[]>(ct) ?? throw Unavailable();
        }
        catch (HttpRequestException) { throw Unavailable(); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw Unavailable(); }
        catch (JsonException) { throw Unavailable(); }
    }
}
