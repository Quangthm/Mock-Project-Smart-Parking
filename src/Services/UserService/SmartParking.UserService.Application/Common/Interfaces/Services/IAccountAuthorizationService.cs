using UserService.Application.Authorization;
using SmartParking.UserService.Domain.Entities;

namespace UserService.Application.Common.Interfaces.Services;

public interface IAccountAuthorizationService
{
    Task<CurrentAccessContext> RequireAdminAsync(Guid userId, bool lockRows, CancellationToken ct);
    Task<CurrentAccessContext> RequireOwnerReviewAsync(Guid userId, bool lockRows, CancellationToken ct);
    Task<IdentityScope> RequireOwnerManagementAsync(Guid userId, bool lockRows, CancellationToken ct);
    Task<IReadOnlyList<DirectorySite>> RequireManagedSitesAsync(Guid userId, Guid[] siteIds, CancellationToken ct);
    Task<IdentityScope> OwnerScopeAsync(OwnerScopeAction action, CancellationToken ct);
    Task<IReadOnlyList<OperatorAssignment>> OperatorAssignmentsAsync(Guid userId, CancellationToken ct);
    Task<IdentityScope> OperatorScopeAsync(Guid siteId, OperatorScopeAction action, CancellationToken ct);
    Task<Guid[]> DriverAccountsAsync(Guid userId, CancellationToken ct);
    Task<CurrentAccessContext> RequireDriverAsync(Guid userId, bool lockRows, CancellationToken ct);
    Task<CurrentAccessContext> RequireOwnSecurityAsync(Guid userId, CancellationToken ct);
    Task RequireDeliveryAsync(Guid userId, WorkflowDelivery delivery, CancellationToken ct);
}

public sealed record IdentityScope(Guid UserId, Guid[] TenantIds);
public enum OwnerScopeAction { ParkingManage, BackupConfigure, BackupMark }
public enum OperatorScopeAction { AssignedSite, SlotOverride }
