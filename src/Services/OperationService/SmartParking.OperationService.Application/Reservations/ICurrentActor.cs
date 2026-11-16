namespace SmartParking.OperationService.Application.Reservations;

public interface ICurrentActor
{
    Guid? UserId { get; }
    Guid? AccountId { get; }
    string? Role { get; }
}
