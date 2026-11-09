using MediatR;
using SmartParking.OperationService.Domain;
using SmartParking.OperationService.Domain.Entities;
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
public sealed record CalculateCheckoutPricingQuery(
    Guid SiteId,
    Guid ReferenceId,
    string ReferenceType,
    string VehicleType,
    DateTimeOffset EntryTime,
    DateTimeOffset ExitTime) : IRequest<CheckoutPricing>;
public sealed record PrepareReservationCheckoutCommand(
    Guid SiteId,
    Guid ReservationId,
    string? PlateNumber) : IRequest<CheckoutPaymentPreparation>;
public sealed record PrepareSessionCheckoutCommand(
    Guid SiteId,
    Guid SessionId,
    string? PlateNumber) : IRequest<CheckoutPaymentPreparation>;

public sealed class CalculateCheckoutPricingQueryHandler(
    IOperatorIdentityClient identity,
    ICheckoutPaymentClient payment) : IRequestHandler<CalculateCheckoutPricingQuery, CheckoutPricing>
{
    public async Task<CheckoutPricing> Handle(CalculateCheckoutPricingQuery request, CancellationToken ct)
    {
        if (request.ReferenceType is not ("RESERVATION" or "PARKING_SESSION"))
            throw new ReservationGateException(400, "INVALID_REFERENCE_TYPE", "Checkout reference type is invalid.");
        if (request.EntryTime > request.ExitTime)
            throw new ReservationGateException(400, "INVALID_TIME_RANGE", "Checkout time range is invalid.");

        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        return await payment.CalculateAsync(
            scope.TenantId,
            request.SiteId,
            request.ReferenceId,
            request.ReferenceType,
            request.VehicleType,
            request.EntryTime,
            request.ExitTime,
            ct);
    }
}

public sealed class PrepareReservationCheckoutCommandHandler(
    IOperatorIdentityClient identity,
    IReservationRepository repository,
    ICheckoutPaymentClient payment) : IRequestHandler<PrepareReservationCheckoutCommand, CheckoutPaymentPreparation>
{
    public async Task<CheckoutPaymentPreparation> Handle(
        PrepareReservationCheckoutCommand request,
        CancellationToken ct)
    {
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        var reservation = await repository.FindAsync(scope.TenantId, request.SiteId, request.ReservationId.ToString(), ct)
            ?? throw new ReservationGateException(404, "RESERVATION_NOT_FOUND", "Reservation was not found.");
        if (reservation.Id != request.ReservationId)
            throw new ReservationGateException(404, "RESERVATION_NOT_FOUND", "Reservation was not found.");

        return await payment.PrepareAsync(
            scope.TenantId,
            request.SiteId,
            reservation.Id,
            "RESERVATION",
            reservation.VehicleType,
            reservation.EntryTime ?? reservation.StartTime,
            DateTimeOffset.UtcNow,
            request.PlateNumber ?? reservation.LicensePlate,
            reservation.AccountId,
            ct);
    }
}

public sealed class PrepareSessionCheckoutCommandHandler(
    IOperatorIdentityClient identity,
    IParkingSessionRepository repository,
    ICheckoutPaymentClient payment) : IRequestHandler<PrepareSessionCheckoutCommand, CheckoutPaymentPreparation>
{
    public async Task<CheckoutPaymentPreparation> Handle(
        PrepareSessionCheckoutCommand request,
        CancellationToken ct)
    {
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        var session = await repository.FindActiveWalkInAsync(
            scope.TenantId,
            request.SiteId,
            request.PlateNumber ?? string.Empty,
            ct);
        if (session is null || session.Id != request.SessionId)
            throw new ReservationGateException(404, "PARKING_SESSION_NOT_FOUND", "Parking session was not found.");

        return await payment.PrepareAsync(
            scope.TenantId,
            request.SiteId,
            session.Id,
            "PARKING_SESSION",
            session.VehicleType,
            session.EntryTime,
            DateTimeOffset.UtcNow,
            request.PlateNumber ?? session.LicensePlate,
            null,
            ct);
    }
}

public sealed class ListReservationsQueryHandler(
    IOperatorIdentityClient identity,
    IReservationRepository repository) : IRequestHandler<ListReservationsQuery, IReadOnlyList<ReservationGateEntry>>
{
    public async Task<IReadOnlyList<ReservationGateEntry>> Handle(ListReservationsQuery request, CancellationToken ct)
    {
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        return await repository.ListAsync(scope.TenantId, request.SiteId, ct);
    }
}

public sealed class ListParkingSessionsQueryHandler(
    IOperatorIdentityClient identity,
    IParkingSessionRepository repository) : IRequestHandler<ListParkingSessionsQuery, IReadOnlyList<ParkingSessionHistoryEntry>>
{
    public async Task<IReadOnlyList<ParkingSessionHistoryEntry>> Handle(ListParkingSessionsQuery request, CancellationToken ct)
    {
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        return await repository.ListSessionsAsync(scope.TenantId, request.SiteId, ct);
    }
}

public sealed class FindReservationQueryHandler(
    IOperatorIdentityClient identity,
    IReservationRepository repository) : IRequestHandler<FindReservationQuery, ReservationGateEntry>
{
    public async Task<ReservationGateEntry> Handle(FindReservationQuery request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Query) || request.Query.Length > 100)
            throw new ReservationGateException(400, "INVALID_QUERY", "Enter a reservation ID or plate.");

        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        return await repository.FindAsync(scope.TenantId, request.SiteId, request.Query.Trim(), ct)
            ?? throw new ReservationGateException(404, "RESERVATION_NOT_FOUND", "No matching reservation was found.");
    }
}

public sealed class FindWalkInSessionQueryHandler(
    IOperatorIdentityClient identity,
    IParkingSessionRepository repository) : IRequestHandler<FindWalkInSessionQuery, WalkInParkingSession>
{
    public async Task<WalkInParkingSession> Handle(FindWalkInSessionQuery request, CancellationToken ct)
    {
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        return await repository.FindActiveWalkInAsync(scope.TenantId, request.SiteId, request.Plate, ct)
            ?? throw new ReservationGateException(404, "WALK_IN_SESSION_NOT_FOUND", "No active walk-in session was found for this plate.");
    }
}
