namespace SmartParking.OperationService.Application.Reservations;

public sealed record BookingView(Guid ReservationId, string State, Guid SiteId, Guid? RequestedSlotId,
    DateTimeOffset Start, DateTimeOffset End, DateTimeOffset? HoldExpiresAt, Guid? OrderId,
    string OrderProvisioningState, string? OrderProvisioningCode, string ZeroPriceAcceptanceState,
    decimal? BookedAmount, decimal? DepositAmount, string? Currency, DateTimeOffset? ConfirmedAt,
    DateTimeOffset CreatedAt)
{
    public Guid? RequestId { get; init; }
    public BookingVehicle? Vehicle { get; init; }
    public string? SiteName { get; init; }
    public string? SiteAddress { get; init; }
    public string? LicensePlate { get; init; }
    public Guid? AllocatedSlotId { get; init; }
    public string? AllocationStatus { get; init; }
    public Guid? ParkingSessionId { get; init; }
    public string? PaymentStatus { get; init; }
}
