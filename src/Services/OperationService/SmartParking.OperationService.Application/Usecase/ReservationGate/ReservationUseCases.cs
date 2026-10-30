using MediatR;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Interfaces.Services;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Application.DTOs;

namespace SmartParking.OperationService.Application.Usecase.ReservationGate;

public sealed record ListReservationsQuery(Guid SiteId) : IRequest<IReadOnlyList<ReservationGateEntry>>;
public sealed record ListParkingSessionsQuery(Guid SiteId) : IRequest<IReadOnlyList<ParkingSessionHistoryEntry>>;
public sealed record FindReservationQuery(Guid SiteId, string Query) : IRequest<ReservationGateEntry>;
public sealed record FindWalkInSessionQuery(Guid SiteId, string Plate) : IRequest<WalkInParkingSession>;
public sealed record CheckInWalkInCommand(Guid SiteId, string PlateNumber, string? ImageData, string? VehicleType)
    : IRequest<WalkInParkingSession>;
public sealed record CheckOutWalkInCommand(Guid SiteId, Guid SessionId, string PlateNumber, string? ImageData)
    : IRequest<WalkInParkingSession>;
public sealed record CheckInReservationCommand(Guid SiteId, Guid ReservationId, string? PlateNumber, string? ImageData)
    : IRequest<ReservationGateEntry>;
public sealed record CheckOutReservationCommand(Guid SiteId, Guid ReservationId, string? PlateNumber, string? ImageData)
    : IRequest<ReservationGateEntry>;

public sealed class ListReservationsQueryHandler(
    IOperatorIdentityClient identity,
    IReservationGateStore store) : IRequestHandler<ListReservationsQuery, IReadOnlyList<ReservationGateEntry>>
{
    public async Task<IReadOnlyList<ReservationGateEntry>> Handle(ListReservationsQuery request, CancellationToken ct)
    {
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        return await store.ListAsync(scope.TenantId, request.SiteId, ct);
    }
}

public sealed class ListParkingSessionsQueryHandler(
    IOperatorIdentityClient identity,
    IReservationGateStore store) : IRequestHandler<ListParkingSessionsQuery, IReadOnlyList<ParkingSessionHistoryEntry>>
{
    public async Task<IReadOnlyList<ParkingSessionHistoryEntry>> Handle(ListParkingSessionsQuery request, CancellationToken ct)
    {
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        return await store.ListSessionsAsync(scope.TenantId, request.SiteId, ct);
    }
}

public sealed class FindReservationQueryHandler(
    IOperatorIdentityClient identity,
    IReservationGateStore store) : IRequestHandler<FindReservationQuery, ReservationGateEntry>
{
    public async Task<ReservationGateEntry> Handle(FindReservationQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query) || request.Query.Length > 100)
            throw new ReservationGateException(400, "INVALID_QUERY", "Enter a reservation ID or plate.");

        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        return await store.FindAsync(scope.TenantId, request.SiteId, request.Query.Trim(), ct)
            ?? throw new ReservationGateException(404, "RESERVATION_NOT_FOUND", "No matching reservation was found.");
    }
}

public sealed class FindWalkInSessionQueryHandler(
    IOperatorIdentityClient identity,
    IReservationGateStore store) : IRequestHandler<FindWalkInSessionQuery, WalkInParkingSession>
{
    public async Task<WalkInParkingSession> Handle(FindWalkInSessionQuery request, CancellationToken ct)
    {
        GateRequestValidation.ValidatePlate(request.Plate, "Enter a valid vehicle plate.");
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        return await store.FindActiveWalkInAsync(scope.TenantId, request.SiteId, request.Plate, ct)
            ?? throw new ReservationGateException(404, "WALK_IN_SESSION_NOT_FOUND", "No active walk-in session was found for this plate.");
    }
}

public sealed class CheckInWalkInCommandHandler(
    IOperatorIdentityClient identity,
    IReservationGateStore store) : IRequestHandler<CheckInWalkInCommand, WalkInParkingSession>
{
    public async Task<WalkInParkingSession> Handle(CheckInWalkInCommand request, CancellationToken ct)
    {
        GateRequestValidation.ValidatePlate(request.PlateNumber, "Enter a valid vehicle plate.");
        GateRequestValidation.ValidateImageData(request.ImageData);
        var vehicleType = GateRequestValidation.NormalizeVehicleType(request.VehicleType);
        if (vehicleType is null)
            throw new ReservationGateException(400, "INVALID_VEHICLE_TYPE", "Vehicle type must be CAR or MOTORCYCLE.");

        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        return await store.CreateWalkInAsync(
            scope.TenantId, request.SiteId, Guid.NewGuid(), vehicleType, request.PlateNumber, request.ImageData, ct);
    }
}

public sealed class CheckOutWalkInCommandHandler(
    IOperatorIdentityClient identity,
    IReservationGateStore store) : IRequestHandler<CheckOutWalkInCommand, WalkInParkingSession>
{
    public async Task<WalkInParkingSession> Handle(CheckOutWalkInCommand request, CancellationToken ct)
    {
        GateRequestValidation.ValidatePlate(request.PlateNumber, "Enter a valid vehicle plate.");
        GateRequestValidation.ValidateImageData(request.ImageData);
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        return await store.CheckOutWalkInAsync(
            scope.TenantId, request.SiteId, request.SessionId, request.PlateNumber, request.ImageData, ct);
    }
}

public sealed class CheckInReservationCommandHandler(
    IOperatorIdentityClient identity,
    IReservationGateStore store) : IRequestHandler<CheckInReservationCommand, ReservationGateEntry>
{
    public async Task<ReservationGateEntry> Handle(CheckInReservationCommand request, CancellationToken ct)
    {
        GateRequestValidation.ValidateReservationAction(request.PlateNumber, request.ImageData);
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        return await store.CheckInAsync(
            scope.TenantId, request.SiteId, scope.UserId, request.ReservationId, request.PlateNumber, request.ImageData, ct);
    }
}

public sealed class CheckOutReservationCommandHandler(
    IOperatorIdentityClient identity,
    IReservationGateStore store) : IRequestHandler<CheckOutReservationCommand, ReservationGateEntry>
{
    public async Task<ReservationGateEntry> Handle(CheckOutReservationCommand request, CancellationToken ct)
    {
        GateRequestValidation.ValidateReservationAction(request.PlateNumber, request.ImageData);
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        return await store.CheckOutAsync(
            scope.TenantId, request.SiteId, scope.UserId, request.ReservationId, request.PlateNumber, request.ImageData, ct);
    }
}

internal static class GateRequestValidation
{
    public static string? NormalizeVehicleType(string? value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            null or "" => "CAR",
            "MOTORCYCLE" or "MOTORBIKE" => "MOTORCYCLE",
            "CAR" => "CAR",
            _ => null
        };

    public static void ValidatePlate(string? plate, string message)
    {
        if (string.IsNullOrWhiteSpace(plate) || plate.Length > 50)
            throw new ReservationGateException(400, "INVALID_PLATE", message);
    }

    public static void ValidateImageData(string? imageData)
    {
        if (imageData is not null &&
            (imageData.Length > 14 * 1024 * 1024 ||
             !(imageData.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase) ||
               imageData.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase) ||
               imageData.StartsWith("data:image/webp;base64,", StringComparison.OrdinalIgnoreCase))))
            throw new ReservationGateException(400, "INVALID_IMAGE", "Gate image must be a JPEG, PNG, or WebP data URL no larger than 14 MB.");
    }

    public static void ValidateReservationAction(string? plate, string? imageData)
    {
        if (plate is { Length: > 50 })
            throw new ReservationGateException(400, "INVALID_PLATE", "Plate number is too long.");
        ValidateImageData(imageData);
    }
}
