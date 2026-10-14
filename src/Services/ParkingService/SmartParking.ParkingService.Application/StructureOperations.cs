namespace SmartParking.ParkingService.Application;

public sealed record StructureEdit(string Action,Guid ResourceId=default,Guid UnitId=default,string? Code=null,string? Name=null,
    string? Address=null,bool Active=false,int Capacity=0,SmartParking.ParkingService.Domain.VehicleType VehicleType=default,
    SmartParking.ParkingService.Domain.SlotType SlotType=default,string? Coordinates3D=null,string? Features=null,decimal? Latitude=null,decimal? Longitude=null);
public sealed record StructureOperation(Guid Id,Guid SiteId,string Status,string? Reason,string? Impact);
public sealed record ReallocationPlan(string Status,string? Reason,string Impact);
public interface IStructureImpactPlanner
{
    Task<ReallocationPlan> PlanAsync(OwnerScope owner,Guid siteId,Guid operationId,StructureEdit edit,CancellationToken ct);
}
public interface IStructureOperationStore
{
    Task<IAsyncDisposable> LockAsync(Guid id,CancellationToken ct);
    Task<StructureOperation> BeginAsync(OwnerScope owner,Guid site,Guid key,StructureEdit edit,CancellationToken ct);
    Task<StructureOperation> UpdateAsync(OwnerScope owner,Guid id,string status,string? reason,string? impact,CancellationToken ct);
    Task<StructureOperation> ReadAsync(OwnerScope owner,Guid site,Guid id,CancellationToken ct);
}
public sealed class StructureOperationService(IStructureOperationStore store,IStructureImpactPlanner planner,ParkingStructureService structure)
{
    public async Task<StructureOperation> ExecuteAsync(OwnerScope owner,Guid site,Guid key,StructureEdit edit,CancellationToken ct)
    {
        if(key==Guid.Empty)throw new SmartParking.ParkingService.Domain.StructureException("INVALID_STRUCTURE","Provide an idempotency key.");
        if(edit.Action is not ("removeSlot" or "removeUnit" or "capacity" or "active" or "moveSlot" or "configureSlot"))throw new SmartParking.ParkingService.Domain.StructureException("INVALID_STRUCTURE","Unsupported impact operation.");
        var operation=await store.BeginAsync(owner,site,key,edit,ct);
        await using var gate=await store.LockAsync(operation.Id,ct);
        operation=await store.ReadAsync(owner,site,operation.Id,ct);
        if(operation.Status=="completed")return operation;
        await structure.GetAsync(owner,site,ct); // Current scope and physical state; dependencies must be available.
        var plan=await planner.PlanAsync(owner,site,operation.Id,edit,ct);
        if(plan.Status!="resolved")return await store.UpdateAsync(owner,operation.Id,plan.Status=="failed"?"failed":"pending",plan.Reason,plan.Impact,ct);
        await store.UpdateAsync(owner,operation.Id,"pending","Impact resolved; awaiting physical commit and projection acknowledgement.",plan.Impact,ct);
        try
        {
            // Recheck under the normal durable fence; a planner response cannot authorize eviction.
            await structure.ChangeAsync(owner,site,s=>
            {
                switch(edit.Action)
                {
                    case "removeSlot":s.RemoveSlot(edit.ResourceId);break;
                    case "removeUnit":s.RemoveUnit(edit.ResourceId);break;
                    case "capacity":s.SetUnitCapacity(edit.ResourceId,edit.Capacity);break;
                    case "active":s.SetActive(edit.Active);break;
                    case "moveSlot":s.MoveSlot(edit.ResourceId,edit.UnitId,edit.Code!);break;
                    case "configureSlot":s.ConfigureSlot(edit.ResourceId,edit.VehicleType,edit.SlotType,edit.Coordinates3D,edit.Features);break;
                }
            },ct,operation.Id);
            return await store.UpdateAsync(owner,operation.Id,"completed",null,plan.Impact,ct);
        }
        catch(SmartParking.ParkingService.Domain.StructureException e)
        {return await store.UpdateAsync(owner,operation.Id,e.Code is "STRUCTURE_IN_USE" or "STRUCTURE_BUSY" or "COMMITMENTS_UNAVAILABLE"?"pending":"failed",e.Message,plan.Impact,ct);}
    }
}
