using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SmartParking.ParkingService.Application;
namespace SmartParking.ParkingService.Infrastructure;
public sealed class StructureImpactPlanner(HttpClient client,IConfiguration config):IStructureImpactPlanner
{
    public async Task<ReallocationPlan> PlanAsync(OwnerScope owner,Guid siteId,Guid operationId,StructureEdit edit,CancellationToken ct)
    {
        try
        {
            using var response=await client.PostAsJsonAsync($"internal/structure/{siteId}/impact",new{tenantId=owner.TenantId},ct);
            if(!response.IsSuccessStatusCode)return new("pending","Reservation impact check is unavailable.","{}");
            var impact=await response.Content.ReadAsStringAsync(ct);using var data=JsonDocument.Parse(impact);
            if(data.RootElement.ValueKind!=JsonValueKind.Object || !data.RootElement.TryGetProperty("reservations",out var reservations) || reservations.ValueKind!=JsonValueKind.Array || !data.RootElement.TryGetProperty("sessions",out var sessions) || sessions.ValueKind!=JsonValueKind.Array)
                return new("failed","Invalid Reservation impact response.","{}");
            if(data.RootElement.GetProperty("reservations").GetArrayLength()==0 && data.RootElement.GetProperty("sessions").GetArrayLength()==0)return new("resolved",null,impact);
            if(data.RootElement.GetProperty("reservations").EnumerateArray().Any(r=>r.GetProperty("status").GetString()!="PENDING_PAYMENT" && r.GetProperty("confirmedAt").ValueKind==JsonValueKind.Null))
                return new("pending","Historical confirmation time is unknown. Reconcile the priority before reallocation.",impact);
            if(!Uri.TryCreate(config["Services:StructureReallocation"],UriKind.Absolute,out var endpoint) || endpoint.Scheme!="https")
                return new("pending","Awaiting Reservation reallocation / Owner exception / Payment refund workflow. Physical structure has not changed.",impact);
            // Owning downstream service receives an immutable operation ID and ordered impact.
            using var planned=await client.PostAsJsonAsync(endpoint,new{operationId,owner.TenantId,siteId,edit,impact=data.RootElement,
                priority="expectedStartThenConfirmation",preference="sameLotThenZone",unfulfillable="ownerExceptionThenRefund"},ct);
            if(!planned.IsSuccessStatusCode)return new("pending","Downstream reallocation has not completed.",impact);
            var result=await planned.Content.ReadFromJsonAsync<ReallocationPlan>(ct);
            return result is {Status:"resolved" or "pending" or "failed"}?result with{Impact=impact}:new("failed","Invalid downstream workflow response.",impact);
        }
        catch(Exception e) when(e is HttpRequestException or JsonException or InvalidOperationException or KeyNotFoundException || e is OperationCanceledException && !ct.IsCancellationRequested)
        {return new("pending","Downstream impact/reallocation is unavailable.","{}");}
    }
}
