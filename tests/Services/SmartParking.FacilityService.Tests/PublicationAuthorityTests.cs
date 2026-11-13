using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using SmartParking.FacilityService.Application;
using SmartParking.FacilityService.Domain;
using SmartParking.FacilityService.Infrastructure;

namespace SmartParking.FacilityService.Tests;

public sealed class PublicationAuthorityTests
{
    [Theory]
    [InlineData(401,"UNAUTHENTICATED")]
    [InlineData(403,"FORBIDDEN")]
    [InlineData(404,"SITE_NOT_FOUND")]
    [InlineData(409,"IDENTITY_UNAVAILABLE")]
    [InlineData(429,"IDENTITY_UNAVAILABLE")]
    [InlineData(500,"IDENTITY_UNAVAILABLE")]
    [InlineData(302,"IDENTITY_UNAVAILABLE")]
    public async Task OwnerFailuresDoNotBecomeApproval(int status, string code)
    {
        using var http = new HttpClient(new Transport(_ => new HttpResponseMessage((HttpStatusCode)status))) { BaseAddress=new("https://synthetic.test/") };
        var authority=new PublicationAuthority(http, Context());
        var error=await Assert.ThrowsAsync<StructureException>(() => authority.RequireApprovedSite(Guid.NewGuid(), default));
        Assert.Equal(code,error.Code);
    }
    [Fact]
    public async Task FixedReadCarriesBearerInHeaderAndSelectorsInBodyOnly()
    {
        var site=Guid.NewGuid();
        var transport=new Transport(request =>
        {
            Assert.Equal("https://synthetic.test/api/auth/owner-scope/publication-evidence",request.RequestUri!.AbsoluteUri);
            Assert.Equal("synthetic-token", request.Headers.Authorization?.Parameter);
            Assert.Equal(HttpMethod.Post,request.Method);
            return new(HttpStatusCode.OK) { Content=JsonContent.Create(new ApprovedSite(site,site,"outdoor")) };
        });
        using var http=new HttpClient(transport) { BaseAddress=new("https://synthetic.test/") };
        Assert.Equal(site,(await new PublicationAuthority(http,Context()).RequireApprovedSite(site,default)).SiteId);
        Assert.Equal(1,transport.Calls);
    }
    [Fact]
    public async Task MissingTokenMalformedBodyTimeoutAndCancellationFailClosed()
    {
        using var http=new HttpClient(new Transport(_ => new(HttpStatusCode.OK) { Content=new StringContent("{broken") })) { BaseAddress=new("https://synthetic.test/") };
        var error=await Assert.ThrowsAsync<StructureException>(() => new PublicationAuthority(http,Context()).RequireApprovedSite(Guid.NewGuid(),default));
        Assert.Equal("IDENTITY_UNAVAILABLE",error.Code);
        var anonymous=new HttpContextAccessor { HttpContext=new DefaultHttpContext() };
        Assert.Equal("UNAUTHENTICATED",(await Assert.ThrowsAsync<StructureException>(() =>
            new PublicationAuthority(http,anonymous).RequireApprovedSite(Guid.NewGuid(),default))).Code);
        using var timeout=new HttpClient(new CancelTransport()) { BaseAddress=new("https://synthetic.test/") };
        Assert.Equal("IDENTITY_UNAVAILABLE",(await Assert.ThrowsAsync<StructureException>(() =>
            new PublicationAuthority(timeout,Context()).RequireApprovedSite(Guid.NewGuid(),default))).Code);
        using var cancelled=new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new PublicationAuthority(timeout,Context()).RequireApprovedSite(Guid.NewGuid(),cancelled.Token));
    }
    private static HttpContextAccessor Context()
    {
        var context=new DefaultHttpContext(); context.Request.Headers.Authorization="Bearer synthetic-token";
        return new() { HttpContext=context };
    }
    private sealed class Transport(Func<HttpRequestMessage,HttpResponseMessage> answer) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        { Calls++; return Task.FromResult(answer(request)); }
    }
    private sealed class CancelTransport : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) =>
            Task.FromException<HttpResponseMessage>(new OperationCanceledException());
    }
}
