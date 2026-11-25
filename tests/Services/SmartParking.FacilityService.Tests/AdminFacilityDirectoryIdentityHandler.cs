using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;

namespace SmartParking.FacilityService.Tests;

// Only the UserService transport is replaced; the real LayoutCallerResolver is retained.
internal sealed class AdminFacilityDirectoryIdentityHandler(ConcurrentQueue<string> calls) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Assert.Equal("identity.test", request.RequestUri!.Host);
        Assert.Equal(HttpMethod.Get, request.Method);
        calls.Enqueue(request.RequestUri.AbsolutePath);
        if (request.RequestUri.AbsolutePath != "/api/auth/me")
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));

        var role = request.Headers.Authorization?.Parameter;
        if (role == "invalid")
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        if (role == "outage")
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        Assert.Contains(role, new[] { "admin", "owner", "driver", "operator" });
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new { success = true, data = new
            {
                userId = Guid.Parse("33333333-3333-3333-3333-333333333333"), role
            } })
        });
    }
}
