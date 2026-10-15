using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SmartParking.ParkingService.Application;
using SmartParking.ParkingService.Domain;

namespace SmartParking.ParkingService.Infrastructure;

public sealed record IdentityScope(Guid UserId, Guid[] TenantIds);
public sealed class OwnerIdentityClient(HttpClient client, IHttpContextAccessor context) : IOwnerAuthorizer
{
    public async Task<IdentityScope> ScopeAsync(CancellationToken ct, string permission = "PARKING_MANAGE")
    {
        var bearer = context.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(bearer)) throw new StructureException("UNAUTHENTICATED", "Sign in as an Owner.");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/owner-scope?permission=" + Uri.EscapeDataString(permission));
            request.Headers.TryAddWithoutValidation("Authorization", bearer);
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) throw new StructureException("UNAUTHENTICATED", "Session is invalid or revoked.");
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden) throw new StructureException("FORBIDDEN", "Owner access is required.");
            if (!response.IsSuccessStatusCode) throw new StructureException("IDENTITY_UNAVAILABLE", "Identity service is unavailable.");
            var result = await response.Content.ReadFromJsonAsync<IdentityScope>(ct);
            return result is { UserId: var id, TenantIds: not null } && id != Guid.Empty ? result : throw new StructureException("IDENTITY_UNAVAILABLE", "Invalid identity response.");
        }
        catch (Exception e) when (e is HttpRequestException or JsonException || e is OperationCanceledException && !ct.IsCancellationRequested)
        { throw new StructureException("IDENTITY_UNAVAILABLE", "Identity service is unavailable."); }
    }
    public async Task<bool> IsOwnerAsync(OwnerScope owner, CancellationToken ct)
    {
        var scope = await ScopeAsync(ct);
        return scope.UserId == owner.UserId && scope.TenantIds.Contains(owner.TenantId);
    }
}


public sealed class StructureCommitmentsClient(HttpClient client) : IStructureCommitments
{
    public async Task<CommitmentSnapshot> AcquireAsync(
        Guid tenantId, Guid siteId, Guid token, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.PutAsJsonAsync(
                $"internal/structure/{siteId}/{token}", new { tenantId }, cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
                throw new StructureException("STRUCTURE_BUSY", "Another edit has a pending hold.");
            if (!response.IsSuccessStatusCode) throw Unavailable();

            var snapshot = await response.Content.ReadFromJsonAsync<CommitmentSnapshot>(cancellationToken);
            return snapshot?.ProtectedSlots is not null ? snapshot : throw Unavailable();
        }
        catch (Exception error) when (IsTransportFailure(error, cancellationToken))
        {
            throw Unavailable();
        }
    }

    public async Task ReleaseAsync(
        Guid tenantId, Guid siteId, Guid token, StructureOutcome? outcome,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.PostAsJsonAsync(
                $"internal/structure/{siteId}/{token}/release",
                new { tenantId, outcome }, cancellationToken);
            if (!response.IsSuccessStatusCode) throw Unavailable();
        }
        catch (Exception error) when (IsTransportFailure(error, cancellationToken))
        {
            throw Unavailable();
        }
    }

    private static bool IsTransportFailure(Exception error, CancellationToken cancellationToken)
        => error is HttpRequestException or JsonException ||
           error is OperationCanceledException && !cancellationToken.IsCancellationRequested;

    private static StructureException Unavailable()
        => new("COMMITMENTS_UNAVAILABLE", "Reservation synchronization is pending; retry after recovery.");
}
