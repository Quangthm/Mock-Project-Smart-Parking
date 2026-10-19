namespace SmartParking.ParkingService.Application;
public sealed record IdentityScope(Guid UserId, Guid[] TenantIds);
public interface IOwnerScopeReader { Task<IdentityScope> ScopeAsync(CancellationToken ct, string permission = "PARKING_MANAGE"); }
