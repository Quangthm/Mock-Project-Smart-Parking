namespace SmartParking.ReservationService.Application;

// Application entry point; the port owns local atomic persistence. Gate business
// corrections (capacity, settlement and occupancy ownership) remain a separate change.
public sealed class ReservationGateService(IReservationGateStore store)
{
    public Task<IReadOnlyList<ReservationGateEntry>> ListAsync(Guid tenantId, Guid siteId, CancellationToken ct) =>
        store.ListAsync(tenantId, siteId, ct);
    public Task<ReservationGateEntry?> FindAsync(Guid tenantId, Guid siteId, string query, CancellationToken ct) =>
        store.FindAsync(tenantId, siteId, query, ct);
    public Task<WalkInParkingSession?> FindActiveWalkInAsync(Guid tenantId, Guid siteId, string plate, CancellationToken ct) =>
        store.FindActiveWalkInAsync(tenantId, siteId, plate, ct);
    public Task<WalkInParkingSession> CreateWalkInAsync(Guid tenantId, Guid siteId, Guid sessionId, string vehicleType, string plate, string? imageData, CancellationToken ct) =>
        store.CreateWalkInAsync(tenantId, siteId, sessionId, vehicleType, plate, imageData, ct);
    public Task<WalkInParkingSession> CheckOutWalkInAsync(Guid tenantId, Guid siteId, Guid sessionId, string plate, string? imageData, CancellationToken ct) =>
        store.CheckOutWalkInAsync(tenantId, siteId, sessionId, plate, imageData, ct);
    public Task<ReservationGateEntry> CheckInAsync(Guid tenantId, Guid siteId, Guid actorId, Guid reservationId, string? recognizedPlate, string? imageData, CancellationToken ct) =>
        store.CheckInAsync(tenantId, siteId, actorId, reservationId, recognizedPlate, imageData, ct);
    public Task<ReservationGateEntry> CheckOutAsync(Guid tenantId, Guid siteId, Guid actorId, Guid reservationId, string? recognizedPlate, string? imageData, CancellationToken ct) =>
        store.CheckOutAsync(tenantId, siteId, actorId, reservationId, recognizedPlate, imageData, ct);
}
