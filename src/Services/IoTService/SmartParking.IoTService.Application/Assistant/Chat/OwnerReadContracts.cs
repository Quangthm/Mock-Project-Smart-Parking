namespace SmartParking.IoTService.Application.Assistant.Chat;

public sealed record ReadToolOptions(bool Vehicles = false, bool Reservations = false, bool Search = false, int SearchMaxAgeSeconds = 0, int SearchSkewSeconds = 0);
public sealed class OwnerReadFailure(string code) : Exception { public string Code { get; } = code; }
public sealed record VehicleSummary(Guid Id, string VehicleType, string DisplayLabel);
public sealed record ReservationVehicle(string VehicleType, string? DisplayLabel);
public sealed record ReservationSummary(Guid Id, Guid SiteId, ReservationVehicle? Vehicle, DateTimeOffset StartAt,
    DateTimeOffset EndAt, string Status, DateTimeOffset AsOf);
public sealed record ReservationPage(ReservationSummary[] Items, string? NextCursor, DateTimeOffset AsOf);
public sealed record ReservationDetail(ReservationSummary Reservation, DateTimeOffset AsOf);
public sealed record VehicleCard(string Type, VehicleSummary[] Items, DateTimeOffset AsOf, bool Truncated);
public sealed record ReservationCard(string Type, ReservationSummary Reservation);
public sealed record ReservationPageCard(string Type, string? NextCursor, DateTimeOffset? From, DateTimeOffset? To, DateTimeOffset AsOf);
public interface IOwnerReadTools
{
    Task<ParkingSearchPage> Search(ParkingSearch query, string? cursor, CancellationToken ct);
    Task<VehicleSummary[]> Vehicles(CancellationToken ct);
    Task<ReservationPage> Reservations(DateTimeOffset? from, DateTimeOffset? to, string? cursor, CancellationToken ct);
    Task<ReservationDetail> Detail(Guid id, CancellationToken ct);
}
