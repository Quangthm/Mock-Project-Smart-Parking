namespace SmartParking.OperationService.Application.Reservations;

public sealed class ReservationAdmissionOptions
{
    public int OverallTimeoutSeconds { get; set; } = 15;
}
