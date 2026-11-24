using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartParking.FacilityService.Application.Common.Interfaces.Services;
using SmartParking.FacilityService.Domain;

namespace SmartParking.FacilityService.Infrastructure;

// Synchronous internal call because enforcement needs an immediate authoritative answer (DE-13):
// the window only becomes effective once Operations names it in an acknowledgement.
public sealed class MaintenanceEnforcementClient(
    HttpClient client,
    ILogger<MaintenanceEnforcementClient>? logger = null) : IMaintenanceEnforcementClient
{
    // Enough to diagnose a refusal without echoing a whole downstream payload.
    private const int MaxLoggedBodyLength = 512;

    public async Task<MaintenanceEnforcementResult> ApplyAsync(
        MaintenanceWindow window, MaintenanceWindowScope scope, CancellationToken ct)
    {
        try
        {
            using var response = await client.PutAsJsonAsync(
                $"internal/structure/{window.SiteId}/maintenance-windows/{window.Id}",
                new
                {
                    tenantId = window.TenantId,
                    unitId = window.UnitId,
                    slotId = window.SlotId,
                    scopedUnitIds = scope.UnitIds,
                    scopedSlotIds = scope.SlotIds,
                    start = Utc(window.Start),
                    end = Utc(window.End),
                    reason = window.Reason
                },
                ct);

            if (response.IsSuccessStatusCode)
            {
                var acknowledgement = await ReadAcknowledgementAsync(response, ct);
                if (acknowledgement is null)
                {
                    await LogDownstreamFailureAsync("acknowledge maintenance window", window, response, ct);
                    return new MaintenanceEnforcementResult.Unavailable();
                }
                return new MaintenanceEnforcementResult.Acknowledged(
                    acknowledgement.EnforcementRef, acknowledgement.AcknowledgedAt.UtcDateTime);
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                var conflict = await ReadConflictAsync(response, ct);
                // Only an overlap rejection is terminal. Any other 409 (e.g. STRUCTURE_BUSY) is
                // transient, so the window stays pending for the retry worker instead of being
                // cancelled locally as if the commitments were real.
                var hasCommitments = conflict.Commitments is { Count: > 0 };
                if (hasCommitments ||
                    string.Equals(conflict.Code, "STRUCTURE_IN_USE", StringComparison.Ordinal))
                {
                    return new MaintenanceEnforcementResult.Conflict(
                        conflict.Commitments ?? [],
                        string.IsNullOrWhiteSpace(conflict.Message)
                            ? "Commitments overlap the maintenance window."
                            : conflict.Message);
                }

                await LogDownstreamFailureAsync("apply maintenance window", window, response, ct);
                return new MaintenanceEnforcementResult.Unavailable();
            }

            await LogDownstreamFailureAsync("apply maintenance window", window, response, ct);
            return new MaintenanceEnforcementResult.Unavailable();
        }
        catch (Exception error) when (IsTransportFailure(error, ct))
        {
            logger?.LogError(
                "Maintenance enforcement request failed for site {SiteId} window {WindowId}: {FailureType}.",
                window.SiteId, window.Id, error.GetType().Name);
            return new MaintenanceEnforcementResult.Unavailable();
        }
    }

    public async Task<bool> CancelAsync(Guid tenantId, Guid siteId, Guid windowId, CancellationToken ct)
    {
        try
        {
            using var response = await client.DeleteAsync(
                $"internal/structure/{siteId}/maintenance-windows/{windowId}?tenantId={tenantId}", ct);
            // A closure Operations never stored is already released remotely.
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound) return true;
            await LogDownstreamFailureAsync("cancel maintenance window", siteId, windowId, response, ct);
            return false;
        }
        catch (Exception error) when (IsTransportFailure(error, ct))
        {
            logger?.LogError(
                "Maintenance cancellation request failed for site {SiteId} window {WindowId}: {FailureType}.",
                siteId, windowId, error.GetType().Name);
            return false;
        }
    }

    private static async Task<Acknowledgement?> ReadAcknowledgementAsync(
        HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var body = await response.Content.ReadFromJsonAsync<Acknowledgement>(ct);
            return body is null || body.EnforcementRef == Guid.Empty ? null : body;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static async Task<ConflictBody> ReadConflictAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ConflictBody>(ct) ?? new ConflictBody(null, null, null);
        }
        catch (JsonException)
        {
            return new ConflictBody(null, null, null);
        }
        catch (NotSupportedException)
        {
            return new ConflictBody(null, null, null);
        }
    }

    private async Task LogDownstreamFailureAsync(
        string operation, MaintenanceWindow window, HttpResponseMessage response, CancellationToken ct) =>
        await LogDownstreamFailureAsync(operation, window.SiteId, window.Id, response, ct);

    private async Task LogDownstreamFailureAsync(
        string operation, Guid siteId, Guid windowId, HttpResponseMessage response, CancellationToken ct)
    {
        if (logger is null) return;
        string body;
        try
        {
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception error) when (IsTransportFailure(error, ct))
        {
            body = "(body unavailable)";
        }
        if (body.Length > MaxLoggedBodyLength) body = body[..MaxLoggedBodyLength];
        // Never log request headers, service keys or tokens.
        logger.LogError(
            "Operations {Operation} at site {SiteId} window {WindowId} failed with status {StatusCode}: {ResponseBody}",
            operation, siteId, windowId, (int)response.StatusCode, body);
    }

    private static bool IsTransportFailure(Exception error, CancellationToken ct) =>
        error is HttpRequestException or JsonException or NotSupportedException ||
        (error is OperationCanceledException && !ct.IsCancellationRequested);

    // Stored window bounds are UTC; serialize them with an explicit zero offset so Operations can
    // bind DateTimeOffset deterministically.
    private static DateTimeOffset Utc(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero);

    private sealed record Acknowledgement(Guid EnforcementRef, DateTimeOffset AcknowledgedAt);

    private sealed record ConflictBody(
        string? Code, string? Message, IReadOnlyList<MaintenanceCommitmentItem>? Commitments);
}