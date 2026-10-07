using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SmartParking.ParkingService.Application;
using SmartParking.ParkingService.Domain;

namespace SmartParking.ParkingService.Infrastructure;

public sealed record IdentityScope(Guid UserId, Guid[] TenantIds);
public sealed class OwnerIdentityClient(HttpClient client, IHttpContextAccessor context) : IOwnerAuthorizer
{
    public async Task<IdentityScope> ScopeAsync(CancellationToken ct)
    {
        var bearer = context.HttpContext?.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(bearer)) throw new StructureException("UNAUTHENTICATED", "Sign in as an Owner.");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "api/auth/owner-scope");
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
    public async Task<IStructureLease> AcquireAsync(Guid tenantId, Guid siteId, CancellationToken ct)
    {
        var token = Guid.NewGuid();
        try
        {
            using var response = await client.PutAsJsonAsync($"internal/structure/{siteId}/{token}", new { tenantId }, ct);
            if (response.StatusCode == System.Net.HttpStatusCode.Conflict) throw new StructureException("STRUCTURE_BUSY", "Another edit is pending. Retry or reconcile its hold.");
            if (!response.IsSuccessStatusCode) throw Unavailable();
            var snapshot = await response.Content.ReadFromJsonAsync<CommitmentSnapshot>(ct) ?? throw Unavailable();
            if (snapshot.ProtectedSlots is null) throw Unavailable();
            return new Lease(client, tenantId, siteId, token, snapshot);
        }
        catch (Exception e) when (e is HttpRequestException or JsonException || e is OperationCanceledException && !ct.IsCancellationRequested) { throw Unavailable(); }
    }
    private static StructureException Unavailable() => new("COMMITMENTS_UNAVAILABLE", "Cannot safely edit structure while Reservation service is unavailable.");
    private sealed class Lease(HttpClient client, Guid tenantId, Guid siteId, Guid token, CommitmentSnapshot snapshot) : IStructureLease
    {
        private bool committing;
        public CommitmentSnapshot Snapshot => snapshot;
        public void BeginCommit() => committing = true;
        public async Task CompleteAsync(StructureOutcome outcome, CancellationToken ct)
        {
            // No automatic release after an ambiguous commit or an unacknowledged projection update.
            committing = true;
            try
            {
                using var response = await client.PostAsJsonAsync($"internal/structure/{siteId}/{token}/release", new { tenantId, outcome }, ct);
                if (!response.IsSuccessStatusCode) throw Unavailable();
            }
            catch (Exception e) when (e is HttpRequestException || e is OperationCanceledException && !ct.IsCancellationRequested) { throw Unavailable(); }
        }
        public async ValueTask DisposeAsync()
        {
            if (committing) return;
            // Only an edit that never began committing may release without publishing a result.
            try { using var response = await client.PostAsJsonAsync($"internal/structure/{siteId}/{token}/release", new { tenantId, outcome = (object?)null }, CancellationToken.None); }
            catch (Exception e) when (e is HttpRequestException or OperationCanceledException) { /* Leave the durable hold closed. */ }
        }
    }
}
