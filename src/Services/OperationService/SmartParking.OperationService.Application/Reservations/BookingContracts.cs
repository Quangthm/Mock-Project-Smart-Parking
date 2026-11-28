using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Entities;

namespace SmartParking.OperationService.Application.Reservations;

internal static class BookingContracts
{
    public static ReservationGateException InvalidOwner() => new(503, "OWNER_CONTRACT_INVALID", "The owner response does not match the reservation contract.");
    public static BookingView View(ReservationRow row) => new(row.Id, row.Status, row.SiteId, row.TargetSlotId,
        row.ExpectedStartTime, row.ExpectedEndTime, row.HoldExpiresAt, row.PaymentOrderId,
        row.BookedAmount, row.DepositAmount, row.Currency, row.ConfirmedAt, row.CreatedAt)
    {
        RequestId = row.RequestId,
        ZeroReceiptId = row.ZeroReceiptId,
        Vehicle = string.IsNullOrWhiteSpace(row.VehicleType) ? null : new(row.VehicleType, row.OriginalPlate ?? row.NormalizedPlate),
        LicensePlate = row.OriginalPlate ?? row.NormalizedPlate
    };

    public static BookingView View(BookingReadResult result) => View(result.Reservation) with
    {
        AllocatedSlotId = result.AllocatedSlotId,
        AllocationStatus = result.AllocationStatus,
        ParkingSessionId = result.ParkingSessionId
    };
}
