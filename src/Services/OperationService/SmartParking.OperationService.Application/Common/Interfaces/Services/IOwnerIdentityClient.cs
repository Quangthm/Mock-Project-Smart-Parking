namespace SmartParking.OperationService.Application.Common.Interfaces.Services;

public sealed record OwnerIdentityScope(Guid UserId, IReadOnlyList<Guid> TenantIds);

public interface IOwnerIdentityClient
{
    Task<OwnerIdentityScope> GetOwnerScopeAsync(string permission = "PARKING_MANAGE", CancellationToken ct = default);
}
