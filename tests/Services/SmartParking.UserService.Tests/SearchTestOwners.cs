using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using SmartParking.OperationService.API;
using SmartParking.OperationService.Application.Discovery;
using SmartParking.OperationService.Domain;

namespace SmartParking.UserService.Tests;

internal sealed class SearchTestOwners(SearchTestClock clock, string? mode) : HttpMessageHandler
{
    public Guid Version = ParkingSearchHttpTests.FirstVersion;
    public bool Empty, StalePrice, StaleFacility, BadPayment, Stall, SawCancellation, Wrap, Loop, Drift, Future;
    public string FarName = "Example", NearName = "Example";
    public decimal FarLatitude = 10.13m;
    public long? FarPrice;
    public long NearPrice = 10000;
    public int CandidateCalls, PriceCalls;
    public int Calls, PaymentStatus, Stalled;
    public TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously);
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Interlocked.Increment(ref Calls);
        if (request.RequestUri!.AbsolutePath == "/internal/discovery/v1/slot-candidates:read")
        {
            Interlocked.Increment(ref CandidateCalls);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(request.Headers.Authorization!.Parameter);
            Assert.Contains(jwt.Claims, c => c.Type == "permission" && c.Value == "discovery:slot-candidates:read");
            var query = await request.Content!.ReadFromJsonAsync<JsonElement>(ct);
            Assert.Equal("CAR", query.GetProperty("vehicleType").GetString());
            Assert.False(query.TryGetProperty("tenantId", out _));
            if (mode == "outage") return new(HttpStatusCode.ServiceUnavailable);
            var slots = Enumerable.Range(0, mode == "oversized" ? 2049 : mode == "empty" ? 0 : 2).Select(_ => Guid.NewGuid()).ToArray();
            var day = query.GetProperty("startAt");
            var items = query.GetProperty("sites").EnumerateArray().Select(s => new
            {
                siteId = mode == "scope" ? Guid.NewGuid() : s.GetProperty("siteId").GetGuid(),
                tenantId = mode == "tenant" ? Guid.Empty : Guid.NewGuid(), configurationVersion = ParkingSearchHttpTests.FirstVersion,
                complete = mode is not (null or "partial" or "coverage") &&
                    (day.ValueKind == JsonValueKind.Null || day.GetDateTimeOffset() < clock.Now.AddDays(1)),
                slots = slots.Select(id => new { slotId = mode == "duplicate" ? slots[0] : Guid.NewGuid(),
                    physicalState = mode == "contradiction" ? "UNKNOWN" : "AVAILABLE", outcome = "ELIGIBLE", slotUpdatedAt = clock.Now })
            });
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { success = true, data = new
                { items, at = query.GetProperty("at"), startAt = day, endAt = query.GetProperty("endAt"),
                  asOf = mode == "stale" ? clock.Now.AddMinutes(-2) : clock.Now } }) };
        }
        Assert.Null(request.Headers.Authorization);
        Assert.False(request.Headers.Contains("X-Account-Id"));
        object data;
        if (request.RequestUri!.Host == "facility.invalid")
        {
            Assert.Equal("/api/public/parking-sites", request.RequestUri.AbsolutePath);
            Assert.Equal(HttpMethod.Get, request.Method);
            Assert.False(request.Headers.Contains("X-Discovery-Service-Key"));
            var first = !request.RequestUri.Query.Contains("cursor=");
            var stamp = StaleFacility ? clock.Now.AddHours(-1) : Future ? clock.Now.AddMinutes(1) : clock.Now;
            object row = new { siteId = first ? ParkingSearchHttpTests.Far : ParkingSearchHttpTests.Near, name = first ? FarName : NearName, address = "Synthetic", latitude = first ? FarLatitude : 10.123456m,
                longitude = 106m, lotType = "outdoor", supportedVehicleTypes = new[] { "CAR", "MOTORCYCLE" }, configurationVersion = ParkingSearchHttpTests.FirstVersion, asOf = stamp,
                serviceWindow = first ? null : new { state = "closed", from = clock.Now.AddDays(-1), to = clock.Now.AddDays(1), asOf = stamp } };
            if (Wrap) row = new { siteId = ParkingSearchHttpTests.Near, name = "Wrap", address = "Synthetic", latitude = 0,
                longitude = -179.999, lotType = "outdoor", supportedVehicleTypes = new[] { "CAR", "MOTORCYCLE" },
                configurationVersion = ParkingSearchHttpTests.FirstVersion, asOf = stamp };
            data = new { items = Empty ? Array.Empty<object>() : new[] { row }, nextCursor = !Empty && !Wrap && (first || Loop) ? "next" : null,
                catalogueVersion = !first && Drift ? Guid.NewGuid() : Version, asOf = stamp };
        }
        else
        {
            Interlocked.Increment(ref PriceCalls);
            Assert.Equal("payment.invalid", request.RequestUri.Host);
            Assert.Equal("/internal/discovery/prices:read", request.RequestUri.AbsolutePath);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(SearchTestHost.Secret, Assert.Single(request.Headers.GetValues("X-Discovery-Service-Key")));
            var body = await request.Content!.ReadFromJsonAsync<JsonElement>(ct);
            Assert.False(body.TryGetProperty("lat", out _));
            Assert.False(body.TryGetProperty("tenantId", out _));
            Assert.False(body.TryGetProperty("accountId", out _));
            if (PaymentStatus != 0) return new((HttpStatusCode)PaymentStatus) { Content = new StringContent("PRIVATE-UPSTREAM-BODY") };
            if (Stall)
            {
                Interlocked.Increment(ref Stalled);
                Started.TrySetResult();
                try { await Task.Delay(Timeout.InfiniteTimeSpan, ct); }
                finally { SawCancellation = ct.IsCancellationRequested; }
            }
            var interval = body.GetProperty("startAt").ValueKind != JsonValueKind.Null;
            var stamp = StalePrice ? clock.Now.AddMinutes(-10) : clock.Now;
            var rows = body.GetProperty("siteIds").EnumerateArray().Select(id => new { siteId = id.GetGuid(),
                price = id.GetGuid() == ParkingSearchHttpTests.Near || FarPrice.HasValue
                    ? new { kind = interval ? "estimate" : "tariff", billingUnit = interval ? "total" : "hour",
                        amountVnd = id.GetGuid() == ParkingSearchHttpTests.Near ? (interval ? 0 : NearPrice) : FarPrice!.Value,
                        policyVersion = 1, asOf = stamp } : null,
                reason = id.GetGuid() == ParkingSearchHttpTests.Near || FarPrice.HasValue ? null : "not_configured" }).ToArray();
            data = new { items = BadPayment ? rows.Take(1).ToArray() : rows, asOf = stamp };
        }
        return new(HttpStatusCode.OK) { Content = JsonContent.Create(new { success = true, data }) };
    }
}
