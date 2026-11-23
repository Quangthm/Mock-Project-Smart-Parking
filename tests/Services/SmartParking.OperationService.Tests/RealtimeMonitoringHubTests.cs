using System.Net;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SmartParking.OperationService.Application.Common.Interfaces.Services;

namespace SmartParking.OperationService.Tests;

public sealed class RealtimeMonitoringHubTests
{
    [Fact]
    public async Task AuthorizedOperator_ConnectsViaWebSocketWithQueryToken_ReceivesSnapshotRequiredAndScopedBroadcasts()
    {
        var tenantId = Guid.NewGuid();
        var siteA = Guid.NewGuid();
        var siteB = Guid.NewGuid();
        var opA = Guid.NewGuid();
        var opB = Guid.NewGuid();

        await using var harness = await RealtimeHubTestHarness.StartAsync((siteId, auth) =>
        {
            if (siteId == siteA) return (StatusCodes.Status200OK, opA, tenantId);
            if (siteId == siteB) return (StatusCodes.Status200OK, opB, tenantId);
            return (StatusCodes.Status403Forbidden, Guid.Empty, Guid.Empty);
        });

        var tokenA = harness.CreateToken(opA, Guid.NewGuid(), "operator");
        var tokenB = harness.CreateToken(opB, Guid.NewGuid(), "operator");

        var (negStatusA, connA, _) = await harness.NegotiateAsync(siteA.ToString(), tokenA);
        var (negStatusB, connB, _) = await harness.NegotiateAsync(siteB.ToString(), tokenB);
        Assert.Equal(HttpStatusCode.OK, negStatusA);
        Assert.Equal(HttpStatusCode.OK, negStatusB);

        // Browser WebSockets send the JWT in ?access_token= without an Authorization header.
        using var wsA = await harness.ConnectWebSocketAsync(siteA.ToString(), connA, queryAccessToken: tokenA);
        using var wsB = await harness.ConnectWebSocketAsync(siteB.ToString(), connB, queryAccessToken: tokenB);
        Assert.Equal(WebSocketState.Open, wsA.State);
        Assert.Equal(WebSocketState.Open, wsB.State);

        var initialA = await harness.ReadSignalRFramesAsync(wsA, maxFrames: 2);
        var initialB = await harness.ReadSignalRFramesAsync(wsB, maxFrames: 2);
        AssertSnapshotRequired(initialA, siteA);
        AssertSnapshotRequired(initialB, siteB);

        var eventId = Guid.NewGuid();
        await harness.BroadcastAsync(new OutboxMessage(
            eventId,
            "SlotStateChanged",
            DateTimeOffset.UtcNow,
            1,
            siteA,
            "{\"slotId\":\"11111111-1111-1111-1111-111111111111\"}"));

        var updateA = await harness.ReadSignalRFramesAsync(wsA, maxFrames: 1, timeout: TimeSpan.FromSeconds(2));
        var updateB = await harness.ReadSignalRFramesAsync(wsB, maxFrames: 1, timeout: TimeSpan.FromMilliseconds(350));

        var frame = Assert.Single(updateA);
        Assert.Equal("SlotStateChanged", frame.RootElement.GetProperty("target").GetString());
        var envelope = frame.RootElement.GetProperty("arguments")[0];
        Assert.Equal(eventId, envelope.GetProperty("eventId").GetGuid());
        Assert.Equal(siteA, envelope.GetProperty("siteId").GetGuid());
        Assert.Empty(updateB);

        DisposeFrames(initialA);
        DisposeFrames(initialB);
        DisposeFrames(updateA);
        DisposeFrames(updateB);
    }

    [Theory]
    [InlineData("operator", true)]
    [InlineData("driver", false)]
    [InlineData("owner", false)]
    public async Task UnassignedSiteAndNonOperatorRoles_AreRejectedWithOperatorLotUnassigned(
        string role,
        bool siteAllowed)
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var harness = await RealtimeHubTestHarness.StartAsync((requestedSite, _) =>
            siteAllowed && requestedSite == siteId
                ? (StatusCodes.Status200OK, userId, tenantId)
                : (StatusCodes.Status403Forbidden, Guid.Empty, Guid.Empty));

        var targetSite = siteAllowed ? Guid.NewGuid() : siteId;
        var token = harness.CreateToken(userId, Guid.NewGuid(), role);
        var (negStatus, connToken, _) = await harness.NegotiateAsync(targetSite.ToString(), token);
        Assert.Equal(HttpStatusCode.OK, negStatus);

        using var ws = await harness.ConnectWebSocketAsync(targetSite.ToString(), connToken, queryAccessToken: token);
        var frames = await harness.ReadSignalRFramesAsync(ws, maxFrames: 5);

        Assert.DoesNotContain(frames, f =>
            f.RootElement.TryGetProperty("target", out var target) &&
            target.GetString() == "SnapshotRequired");
        var closeFrame = Assert.Single(frames, f =>
            f.RootElement.TryGetProperty("type", out var type) && type.GetInt32() == 7);
        Assert.Contains("OPERATOR_LOT_UNASSIGNED", closeFrame.RootElement.GetProperty("error").GetString());

        DisposeFrames(frames);
    }

    [Fact]
    public async Task ExpiredOrMissingJwt_IsRejectedWithoutJoiningSiteGroup()
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var rejectIdentityAsUnauthenticated = false;

        await using var harness = await RealtimeHubTestHarness.StartAsync((requestedSite, _) =>
            !rejectIdentityAsUnauthenticated && requestedSite == siteId
                ? (StatusCodes.Status200OK, userId, tenantId)
                : (StatusCodes.Status401Unauthorized, Guid.Empty, Guid.Empty));

        var expiredToken = harness.CreateToken(userId, Guid.NewGuid(), "operator", TimeSpan.FromMinutes(-5));
        var validToken = harness.CreateToken(userId, Guid.NewGuid(), "operator");

        var (expiredNegStatus, _, expiredNegBody) = await harness.NegotiateAsync(siteId.ToString(), expiredToken);
        Assert.Equal(HttpStatusCode.Unauthorized, expiredNegStatus);
        Assert.Contains("UNAUTHENTICATED", expiredNegBody);

        var (missingNegStatus, _, missingNegBody) = await harness.NegotiateAsync(siteId.ToString(), bearerToken: null);
        Assert.Equal(HttpStatusCode.Unauthorized, missingNegStatus);
        Assert.Contains("UNAUTHENTICATED", missingNegBody);

        var (validNegStatus, connForExpired, _) = await harness.NegotiateAsync(siteId.ToString(), validToken);
        Assert.Equal(HttpStatusCode.OK, validNegStatus);
        await Assert.ThrowsAnyAsync<WebSocketException>(() =>
            harness.ConnectWebSocketAsync(siteId.ToString(), connForExpired, queryAccessToken: expiredToken));

        var (_, connForMissing, _) = await harness.NegotiateAsync(siteId.ToString(), validToken);
        await Assert.ThrowsAnyAsync<WebSocketException>(() =>
            harness.ConnectWebSocketAsync(siteId.ToString(), connForMissing, queryAccessToken: null));

        // Even a direct WebSocket upgrade skipping /negotiate must fail closed on an expired token.
        await Assert.ThrowsAnyAsync<WebSocketException>(() =>
            harness.ConnectWebSocketAsync(siteId.ToString(), connectionToken: null, queryAccessToken: expiredToken));

        // If JWT validates locally but Identity rejects the session (401), hub must close with UNAUTHENTICATED.
        rejectIdentityAsUnauthenticated = true;
        var (_, revokedConn, _) = await harness.NegotiateAsync(siteId.ToString(), validToken);
        using var revokedWs = await harness.ConnectWebSocketAsync(siteId.ToString(), revokedConn, queryAccessToken: validToken);
        var revokedFrames = await harness.ReadSignalRFramesAsync(revokedWs, maxFrames: 5);
        Assert.DoesNotContain(revokedFrames, f =>
            f.RootElement.TryGetProperty("target", out var target) &&
            target.GetString() == "SnapshotRequired");
        var closeFrame = Assert.Single(revokedFrames, f =>
            f.RootElement.TryGetProperty("type", out var type) && type.GetInt32() == 7);
        Assert.Contains("UNAUTHENTICATED", closeFrame.RootElement.GetProperty("error").GetString());

        DisposeFrames(revokedFrames);
    }

    [Fact]
    public async Task LongPollingClonedContext_ResolvesSiteFromPath_AndRejectsInvalidSiteId()
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var harness = await RealtimeHubTestHarness.StartAsync((requestedSite, _) =>
            requestedSite == siteId
                ? (StatusCodes.Status200OK, userId, tenantId)
                : (StatusCodes.Status403Forbidden, Guid.Empty, Guid.Empty));

        var token = harness.CreateToken(userId, Guid.NewGuid(), "operator");

        var (negStatus, connToken, _) = await harness.NegotiateAsync(siteId.ToString(), token);
        Assert.Equal(HttpStatusCode.OK, negStatus);

        var (firstGet, postStatus, pollBody) = await harness.LongPollExchangeAsync(siteId.ToString(), connToken, token);
        Assert.Equal(HttpStatusCode.OK, firstGet);
        Assert.Equal(HttpStatusCode.OK, postStatus);
        Assert.Contains("SnapshotRequired", pollBody);
        Assert.Contains(siteId.ToString(), pollBody);
        Assert.DoesNotContain("INVALID_SITE_ID", pollBody);

        foreach (var badSite in new[] { Guid.Empty.ToString(), "not-a-guid" })
        {
            var (badNegStatus, badConn, _) = await harness.NegotiateAsync(badSite, token);
            Assert.Equal(HttpStatusCode.OK, badNegStatus);
            using var ws = await harness.ConnectWebSocketAsync(badSite, badConn, queryAccessToken: token);
            var frames = await harness.ReadSignalRFramesAsync(ws, maxFrames: 5);
            var closeFrame = Assert.Single(frames, f =>
                f.RootElement.TryGetProperty("type", out var type) && type.GetInt32() == 7);
            Assert.Contains("INVALID_SITE_ID", closeFrame.RootElement.GetProperty("error").GetString());
            DisposeFrames(frames);
        }
    }

    [Fact]
    public async Task UnconfiguredJwtValidation_FailsClosedWithAuthenticationKeysUnavailable()
    {
        var siteId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var identityCalls = 0;

        await using var harness = await RealtimeHubTestHarness.StartAsync((requestedSite, _) =>
        {
            identityCalls++;
            return requestedSite == siteId
                ? (StatusCodes.Status200OK, userId, tenantId)
                : (StatusCodes.Status403Forbidden, Guid.Empty, Guid.Empty);
        }, configureJwt: false);

        var token = harness.CreateToken(userId, Guid.NewGuid(), "operator");
        var (status, _, body) = await harness.NegotiateAsync(siteId.ToString(), token);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, status);
        Assert.Contains("AUTHENTICATION_KEYS_UNAVAILABLE", body);

        await Assert.ThrowsAnyAsync<WebSocketException>(() =>
            harness.ConnectWebSocketAsync(siteId.ToString(), connectionToken: null, queryAccessToken: token));
        Assert.Equal(0, identityCalls);
    }

    [Fact]
    public async Task EstablishedConnection_ClosesWhenTokenExpires_AndRejoinsAfterRefresh()
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using var harness = await RealtimeHubTestHarness.StartAsync((requestedSite, _) =>
            requestedSite == siteId
                ? (StatusCodes.Status200OK, userId, tenantId)
                : (StatusCodes.Status403Forbidden, Guid.Empty, Guid.Empty),
            clockSkewSeconds: 0);

        var shortToken = harness.CreateToken(userId, Guid.NewGuid(), "operator", TimeSpan.FromSeconds(2));
        var (negStatus, connToken, _) = await harness.NegotiateAsync(siteId.ToString(), shortToken);
        Assert.Equal(HttpStatusCode.OK, negStatus);

        using var ws1 = await harness.ConnectWebSocketAsync(siteId.ToString(), connToken, queryAccessToken: shortToken);
        var initialFrames = await harness.ReadSignalRFramesAsync(ws1, maxFrames: 2);
        AssertSnapshotRequired(initialFrames, siteId);
        DisposeFrames(initialFrames);

        // Wait past token expiration and SignalR's 1s authentication-expiration check.
        var postExpiryFrames = await harness.ReadSignalRFramesAsync(ws1, maxFrames: 4, timeout: TimeSpan.FromSeconds(6));
        Assert.True(
            ws1.State != WebSocketState.Open ||
            postExpiryFrames.Any(f => f.RootElement.TryGetProperty("type", out var t) && t.GetInt32() == 7),
            "Expected SignalR server to close the connection after token expiry.");
        DisposeFrames(postExpiryFrames);

        // Refresh token and rejoin cleanly with SnapshotRequired reconciliation + live delivery.
        var refreshedToken = harness.CreateToken(userId, Guid.NewGuid(), "operator", TimeSpan.FromMinutes(10));
        var (negStatus2, connToken2, _) = await harness.NegotiateAsync(siteId.ToString(), refreshedToken);
        Assert.Equal(HttpStatusCode.OK, negStatus2);

        using var ws2 = await harness.ConnectWebSocketAsync(siteId.ToString(), connToken2, queryAccessToken: refreshedToken);
        var rejoinFrames = await harness.ReadSignalRFramesAsync(ws2, maxFrames: 2);
        AssertSnapshotRequired(rejoinFrames, siteId);
        DisposeFrames(rejoinFrames);

        var eventId = Guid.NewGuid();
        await harness.BroadcastAsync(new OutboxMessage(
            eventId,
            "SlotStateChanged",
            DateTimeOffset.UtcNow,
            2,
            siteId,
            "{\"slotId\":\"22222222-2222-2222-2222-222222222222\"}"));

        var postRefreshBroadcast = await harness.ReadSignalRFramesAsync(ws2, maxFrames: 1, timeout: TimeSpan.FromSeconds(2));
        var msg = Assert.Single(postRefreshBroadcast);
        Assert.Equal("SlotStateChanged", msg.RootElement.GetProperty("target").GetString());
        Assert.Equal(eventId, msg.RootElement.GetProperty("arguments")[0].GetProperty("eventId").GetGuid());
        DisposeFrames(postRefreshBroadcast);
    }

    [Fact]
    public async Task SharedMultiSiteHub_WhenDisabledByDefault_FailsClosedWhileOperatorSingleSiteHubRemainsAvailable()
    {
        var tenantId = Guid.NewGuid();
        var siteId = Guid.NewGuid();
        var opId = Guid.NewGuid();

        await using var harness = await RealtimeHubTestHarness.StartAsync((requestedSite, _) =>
            requestedSite == siteId
                ? (StatusCodes.Status200OK, opId, tenantId)
                : (StatusCodes.Status403Forbidden, Guid.Empty, Guid.Empty));

        var ownerToken = harness.CreateToken(Guid.NewGuid(), Guid.NewGuid(), "owner");
        var (sharedStatus, _, sharedBody) = await harness.NegotiateSharedAsync(ownerToken);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, sharedStatus);
        Assert.Contains("REALTIME_SHARED_HUB_DISABLED", sharedBody, StringComparison.Ordinal);

        await Assert.ThrowsAnyAsync<WebSocketException>(() =>
            harness.ConnectSharedWebSocketAsync(connectionToken: null, queryAccessToken: ownerToken));

        var operatorToken = harness.CreateToken(opId, Guid.NewGuid(), "operator");
        var (opNegStatus, opConnToken, _) = await harness.NegotiateAsync(siteId.ToString(), operatorToken);
        Assert.Equal(HttpStatusCode.OK, opNegStatus);
        using var ws = await harness.ConnectWebSocketAsync(siteId.ToString(), opConnToken, queryAccessToken: operatorToken);
        var frames = await harness.ReadSignalRFramesAsync(ws, maxFrames: 2);
        AssertSnapshotRequired(frames, siteId);
        DisposeFrames(frames);
    }

    [Fact]
    public async Task OwnerSharedHub_EnforcesCumulative20SiteLimit_IdempotentDetail_CrossTenantDenial_AndExcludesBookingsOnly()
    {
        var ownerTenant = Guid.NewGuid();
        var otherTenant = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var ownedSites = Enumerable.Range(0, 21).Select(_ => Guid.NewGuid()).ToArray();
        var unownedSite = Guid.NewGuid();
        var facilityCatalog = ownedSites.Take(20).Select(id => (id, ownerTenant))
            .Concat([(unownedSite, otherTenant)])
            .ToArray();

        await using var harness = await RealtimeHubTestHarness.StartAsync(
            ownerScopeHandler: _ => (StatusCodes.Status200OK, ownerId, [ownerTenant], facilityCatalog),
            enableSharedHub: true);

        var ownerToken = harness.CreateToken(ownerId, Guid.NewGuid(), "owner");
        var (negStatus, connToken, _) = await harness.NegotiateSharedAsync(ownerToken);
        Assert.Equal(HttpStatusCode.OK, negStatus);

        using var ws = await harness.ConnectSharedWebSocketAsync(connToken, ownerToken);
        var handshakeFrames = await harness.ReadSignalRFramesAsync(ws, maxFrames: 1);
        DisposeFrames(handshakeFrames);

        // Subscribe first 18 owned sites.
        await harness.InvokeHubAsync(ws, "inv-1", "SubscribeOwnerSites", [ownedSites.Take(18).ToArray(), null]);
        var frames1 = await harness.ReadUntilCompletionAsync(ws, "inv-1");
        Assert.Equal(18, frames1.Count(f => f.RootElement.TryGetProperty("target", out var t) && t.GetString() == "SnapshotRequired"));
        var comp1 = Assert.Single(frames1, f => f.RootElement.TryGetProperty("type", out var t) && t.GetInt32() == 3);
        Assert.False(comp1.RootElement.TryGetProperty("error", out _));
        DisposeFrames(frames1);

        // Cumulative subscribe adds sites 19 and 20 + selects site 0 -> total 20 distinct sites allowed.
        await harness.InvokeHubAsync(ws, "inv-2", "SubscribeOwnerSites", [ownedSites.Skip(18).Take(2).ToArray(), ownedSites[0]]);
        var frames2 = await harness.ReadUntilCompletionAsync(ws, "inv-2");
        Assert.Equal(2, frames2.Count(f => f.RootElement.TryGetProperty("target", out var t) && t.GetString() == "SnapshotRequired"));
        var comp2 = Assert.Single(frames2, f => f.RootElement.TryGetProperty("type", out var t) && t.GetInt32() == 3);
        Assert.False(comp2.RootElement.TryGetProperty("error", out _));
        DisposeFrames(frames2);

        // Detail enrichment on an already-subscribed site is idempotent and does not consume slot 21.
        await harness.InvokeHubAsync(ws, "inv-3", "SubscribeOwnerSites", [new[] { ownedSites[0] }, ownedSites[0]]);
        var frames3 = await harness.ReadUntilCompletionAsync(ws, "inv-3");
        var comp3 = Assert.Single(frames3, f => f.RootElement.TryGetProperty("type", out var t) && t.GetInt32() == 3);
        Assert.False(comp3.RootElement.TryGetProperty("error", out _));
        DisposeFrames(frames3);

        // Selecting a 21st distinct site is rejected cumulatively on the server.
        await harness.InvokeHubAsync(ws, "inv-4", "SubscribeOwnerSites", [Array.Empty<Guid>(), ownedSites[20]]);
        var frames4 = await harness.ReadUntilCompletionAsync(ws, "inv-4");
        var comp4 = Assert.Single(frames4, f => f.RootElement.TryGetProperty("type", out var t) && t.GetInt32() == 3);
        Assert.Contains("OWNER_SITE_LIMIT_EXCEEDED", comp4.RootElement.GetProperty("error").GetString());
        DisposeFrames(frames4);

        // Unsubscribe 1 site so count is 19, then attempt an unowned cross-tenant site -> denied with OWNER_SITE_UNAUTHORIZED.
        await harness.InvokeHubAsync(ws, "inv-5", "UnsubscribeOwnerSites", [new[] { ownedSites[19] }]);
        DisposeFrames(await harness.ReadUntilCompletionAsync(ws, "inv-5"));

        await harness.InvokeHubAsync(ws, "inv-6", "SubscribeOwnerSites", [new[] { unownedSite }, null]);
        var frames6 = await harness.ReadUntilCompletionAsync(ws, "inv-6");
        var comp6 = Assert.Single(frames6, f => f.RootElement.TryGetProperty("type", out var t) && t.GetInt32() == 3);
        Assert.Contains("OWNER_SITE_UNAUTHORIZED", comp6.RootElement.GetProperty("error").GetString());
        DisposeFrames(frames6);

        // Operations-only bookings invalidation must not push to owners, while subsequent Facility layout invalidation does.
        var bookingsEventId = Guid.NewGuid();
        await harness.BroadcastAsync(new OutboxMessage(
            bookingsEventId,
            "SnapshotRequired",
            DateTimeOffset.UtcNow,
            0,
            ownedSites[0],
            "{\"affectedResources\":[\"bookings\"]}"));

        var layoutEventId = Guid.NewGuid();
        await harness.BroadcastAsync(new OutboxMessage(
            layoutEventId,
            "SnapshotRequired",
            DateTimeOffset.UtcNow,
            0,
            ownedSites[0],
            $"{{\"tenantId\":\"{ownerTenant}\",\"affectedResources\":[\"full-layout\",\"capacity-summary\"]}}"));
        var ownerFrames = await harness.ReadSignalRFramesAsync(ws, maxFrames: 1, timeout: TimeSpan.FromSeconds(2));
        var ownerMsg = Assert.Single(ownerFrames);
        Assert.Equal("SnapshotRequired", ownerMsg.RootElement.GetProperty("target").GetString());
        Assert.Equal(layoutEventId, ownerMsg.RootElement.GetProperty("arguments")[0].GetProperty("eventId").GetGuid());
        DisposeFrames(ownerFrames);
    }

    [Fact]
    public async Task DriverSharedHub_Enforces10Plus1Limit_Discoverability_AndRedactsSensitiveFields()
    {
        var driverId = Guid.NewGuid();
        var visibleSites = Enumerable.Range(0, 11).Select(_ => Guid.NewGuid()).ToArray();
        var selectedSite = Guid.NewGuid();
        var hiddenSite = Guid.NewGuid();
        var tenantId = Guid.NewGuid();

        await using var harness = await RealtimeHubTestHarness.StartAsync(
            driverLayoutHandler: (siteId, _) => siteId == hiddenSite ? StatusCodes.Status404NotFound : StatusCodes.Status200OK,
            enableSharedHub: true);

        // Anonymous callers cannot negotiate the shared hub.
        var (anonStatus, _, _) = await harness.NegotiateSharedAsync(bearerToken: null);
        Assert.Equal(HttpStatusCode.Unauthorized, anonStatus);

        var driverToken = harness.CreateToken(driverId, Guid.NewGuid(), "driver");
        var (negStatus, connToken, _) = await harness.NegotiateSharedAsync(driverToken);
        Assert.Equal(HttpStatusCode.OK, negStatus);

        using var ws = await harness.ConnectSharedWebSocketAsync(connToken, driverToken);
        DisposeFrames(await harness.ReadSignalRFramesAsync(ws, maxFrames: 1));

        // More than 10 visible sites is rejected server-side.
        await harness.InvokeHubAsync(ws, "drv-over", "SubscribeDriverSites", [visibleSites, selectedSite]);
        var overFrames = await harness.ReadUntilCompletionAsync(ws, "drv-over");
        var overComp = Assert.Single(overFrames, f => f.RootElement.TryGetProperty("type", out var t) && t.GetInt32() == 3);
        Assert.Contains("DRIVER_SITE_LIMIT_EXCEEDED", overComp.RootElement.GetProperty("error").GetString());
        DisposeFrames(overFrames);

        // Hidden / inactive site is rejected via the Driver REST read contract.
        await harness.InvokeHubAsync(ws, "drv-hidden", "SubscribeDriverSites", [new[] { hiddenSite }, null]);
        var hiddenFrames = await harness.ReadUntilCompletionAsync(ws, "drv-hidden");
        var hiddenComp = Assert.Single(hiddenFrames, f => f.RootElement.TryGetProperty("type", out var t) && t.GetInt32() == 3);
        Assert.Contains("SITE_NOT_DISCOVERABLE", hiddenComp.RootElement.GetProperty("error").GetString());
        DisposeFrames(hiddenFrames);

        // 10 visible summary sites + 1 distinct selected detail site succeeds.
        await harness.InvokeHubAsync(ws, "drv-ok", "SubscribeDriverSites", [visibleSites.Take(10).ToArray(), selectedSite]);
        var okFrames = await harness.ReadUntilCompletionAsync(ws, "drv-ok");
        Assert.Equal(11, okFrames.Count(f => f.RootElement.TryGetProperty("target", out var t) && t.GetString() == "SnapshotRequired"));
        var okComp = Assert.Single(okFrames, f => f.RootElement.TryGetProperty("type", out var t) && t.GetInt32() == 3);
        Assert.False(okComp.RootElement.TryGetProperty("error", out _));
        DisposeFrames(okFrames);

        // Broadcast with sensitive fields -> Driver receives redacted envelope with capacity-summary + sanitized-detail only.
        var evtId = Guid.NewGuid();
        await harness.BroadcastAsync(new OutboxMessage(
            evtId,
            "SlotStateChanged",
            DateTimeOffset.UtcNow,
            5,
            selectedSite,
            $"{{\"tenantId\":\"{tenantId}\",\"slotId\":\"33333333-3333-3333-3333-333333333333\",\"licensePlate\":\"51A12345\",\"sessionId\":\"sec-1\"}}"));

        var pushFrames = await harness.ReadSignalRFramesAsync(ws, maxFrames: 1, timeout: TimeSpan.FromSeconds(2));
        var msg = Assert.Single(pushFrames);
        Assert.Equal("SnapshotRequired", msg.RootElement.GetProperty("target").GetString());
        var envelope = msg.RootElement.GetProperty("arguments")[0];
        Assert.Equal(evtId, envelope.GetProperty("eventId").GetGuid());
        Assert.Equal(1, envelope.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(selectedSite, envelope.GetProperty("siteId").GetGuid());

        var rawEnvelopeText = envelope.GetRawText();
        Assert.DoesNotContain("tenantId", rawEnvelopeText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("slotId", rawEnvelopeText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("licensePlate", rawEnvelopeText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("51A12345", rawEnvelopeText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sessionId", rawEnvelopeText, StringComparison.OrdinalIgnoreCase);

        var resources = envelope.GetProperty("payload").GetProperty("affectedResources")
            .EnumerateArray().Select(e => e.GetString()!).ToArray();
        Assert.Equal(new[] { "capacity-summary", "sanitized-detail" }, resources);
        DisposeFrames(pushFrames);
    }

    private static void AssertSnapshotRequired(IReadOnlyList<JsonDocument> frames, Guid expectedSiteId)
    {
        Assert.Contains(frames, f =>
            f.RootElement.GetRawText() == "{}" ||
            (f.RootElement.TryGetProperty("type", out _) is false && !f.RootElement.TryGetProperty("error", out _)));
        var snapshot = Assert.Single(frames, f =>
            f.RootElement.TryGetProperty("target", out var target) &&
            target.GetString() == "SnapshotRequired");
        Assert.Equal(expectedSiteId, snapshot.RootElement.GetProperty("arguments")[0].GetGuid());
    }

    private static void DisposeFrames(IEnumerable<JsonDocument> frames)
    {
        foreach (var frame in frames) frame.Dispose();
    }
}
