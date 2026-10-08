using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SmartParking.ParkingService.Application;
using SmartParking.ParkingService.Domain;
namespace SmartParking.ParkingService.Infrastructure;
public sealed class SlotOperatorClient(HttpClient client,IHttpContextAccessor context):ISlotOperatorAuthorizer
{
    public async Task<OwnerScope> ScopeAsync(Guid site,CancellationToken ct)
    {
        var bearer=context.HttpContext?.Request.Headers.Authorization.ToString();
        if(string.IsNullOrWhiteSpace(bearer))throw new StructureException("UNAUTHENTICATED","Sign in as an assigned Operator.");
        try
        {
            using var request=new HttpRequestMessage(HttpMethod.Get,$"api/operators/slot-scope?siteId={site}");request.Headers.TryAddWithoutValidation("Authorization",bearer);
            using var response=await client.SendAsync(request,ct);
            if(response.StatusCode==HttpStatusCode.Unauthorized)throw new StructureException("UNAUTHENTICATED","Session is invalid or revoked.");
            if(response.StatusCode==HttpStatusCode.Forbidden)throw new StructureException("FORBIDDEN","Explicit SLOT_OVERRIDE permission at this lot is required.");
            if(!response.IsSuccessStatusCode)throw new StructureException("IDENTITY_UNAVAILABLE","Operator authority is unavailable.");
            var scope=await response.Content.ReadFromJsonAsync<IdentityScope>(ct);
            return scope is {UserId:var id,TenantIds:{Length:1}} && id!=Guid.Empty?new(id,scope.TenantIds[0]):throw new StructureException("IDENTITY_UNAVAILABLE","Invalid Operator scope.");
        }
        catch(Exception e) when(e is HttpRequestException or JsonException || e is OperationCanceledException && !ct.IsCancellationRequested)
        {throw new StructureException("IDENTITY_UNAVAILABLE","Operator authority is unavailable.");}
    }
    public async Task<bool> IsAuthorizedAsync(OwnerScope actor,Guid site,CancellationToken ct)=>actor==await ScopeAsync(site,ct);
}
