namespace SmartParking.PaymentService.Application.Common.Interfaces;

public interface ICurrentActor
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    Guid? AccountId => UserId;
    string? Role { get; }
    Guid? TenantId { get; }
    Guid? SiteId { get; }
    bool IsInternalService { get; }
}
