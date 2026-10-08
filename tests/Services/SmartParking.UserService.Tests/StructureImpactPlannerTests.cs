using System.Net;
using Microsoft.Extensions.Configuration;
using SmartParking.ParkingService.Application;
using SmartParking.ParkingService.Infrastructure;
namespace SmartParking.UserService.Tests;
public sealed class StructureImpactPlannerTests
{
    private sealed class Handler(string body):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
            =>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body,System.Text.Encoding.UTF8,"application/json")});
    }
    [Theory]
    [InlineData("{}","failed")]
    [InlineData("{\"reservations\":[],\"sessions\":[]}","resolved")]
    [InlineData("{\"reservations\":[{\"status\":\"CONFIRMED\",\"confirmedAt\":null}],\"sessions\":[]}","pending")]
    [InlineData("{\"reservations\":[{\"status\":\"CONFIRMED\",\"confirmedAt\":\"2026-10-06T10:00:00Z\"}],\"sessions\":[]}","pending")]
    public async Task IncompleteDependencyOrHistoricalPriorityNeverResolves(string body,string expected)
    {
        using var http=new HttpClient(new Handler(body)){BaseAddress=new("http://reservation.test/")};
        var planner=new StructureImpactPlanner(http,new ConfigurationBuilder().Build());
        var result=await planner.PlanAsync(new(Guid.NewGuid(),Guid.NewGuid()),Guid.NewGuid(),Guid.NewGuid(),new("active",Active:false),default);
        Assert.Equal(expected,result.Status);
    }
}
