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
public sealed record ScanParkingSessionCommand(Guid SiteId, Guid ReferenceId, Guid SlotId) : IRequest<ParkingSessionHistoryEntry>;
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
    string? PlateNumber,
    decimal? DevelopmentTestAmount = null) : IRequest<CheckoutPaymentPreparation>;

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
    ICheckoutPaymentClient payment,
    IParkingSessionRepository sessions,
    IOperationsConcurrency guards,
    IUnitOfWork work,
    TimeProvider clock) : IRequestHandler<PrepareReservationCheckoutCommand, CheckoutPaymentPreparation>
{
    public async Task<CheckoutPaymentPreparation> Handle(
        PrepareReservationCheckoutCommand request,
        CancellationToken ct)
    {
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        var session = await sessions.FindByReservationAsync(scope.TenantId, request.SiteId, request.ReservationId, true, ct)
            ?? throw new ReservationGateException(404, "ACTIVE_SESSION_NOT_FOUND", "No active parking session was found for this reservation.");
        GateRules.VerifyPlate(session.OriginalPlate ?? session.NormalizedPlate ?? "", request.PlateNumber);
        var result = await payment.PrepareAsync(
            scope.TenantId,
            request.SiteId,
            request.ReservationId,
            "RESERVATION",
            session.VehicleType,
            session.EntryTime,
            clock.GetUtcNow(),
            session.OriginalPlate ?? session.NormalizedPlate ?? "",
            session.AccountId,
            ct);
        await GateCheckout.BindOrderAsync(sessions, guards, work, session, result.Order.OrderId, ct);
        return result;
    }
}

public sealed class PrepareSessionCheckoutCommandHandler(
    IOperatorIdentityClient identity,
    IParkingSessionRepository sessions,
    ICheckoutPaymentClient payment,
    IOperationsConcurrency guards,
    IUnitOfWork work,
    TimeProvider clock) : IRequestHandler<PrepareSessionCheckoutCommand, CheckoutPaymentPreparation>
{
    public async Task<CheckoutPaymentPreparation> Handle(
        PrepareSessionCheckoutCommand request,
        CancellationToken ct)
    {
        var scope = await identity.RequireSiteAsync(request.SiteId, ct);
        var session = await sessions.FindForUpdateAsync(scope.TenantId, request.SiteId, request.SessionId, ct);
        if (session is null || session.ReservationId is not null || session.ExitTime is not null)
            throw new ReservationGateException(404, "PARKING_SESSION_NOT_FOUND", "Parking session was not found.");
        GateRules.VerifyPlate(session.OriginalPlate ?? session.NormalizedPlate ?? "", request.PlateNumber);
        var result = await payment.PrepareAsync(
            scope.TenantId,
            request.SiteId,
            session.Id,
            "PARKING_SESSION",
            session.VehicleType,
            session.EntryTime,
            clock.GetUtcNow(),
            session.OriginalPlate ?? session.NormalizedPlate ?? "",
            null,
            ct,
            request.DevelopmentTestAmount);
        await GateCheckout.BindOrderAsync(sessions, guards, work, session, result.Order.OrderId, ct);
        return result;
    }
}

internal static class GateCheckout
{
    public static async Task BindOrderAsync(IParkingSessionRepository sessions, IOperationsConcurrency guards,
        IUnitOfWork work, ParkingSessionRow observed, Guid orderId, CancellationToken ct)
    {
        await using var transaction = await work.BeginTransactionAsync(ct);
        await guards.LockVehicleAsync(observed.NormalizedPlate!, ct);
        await guards.LockSiteAsync(observed.SiteId, ct);
        var session = await sessions.FindForUpdateAsync(observed.TenantId, observed.SiteId, observed.Id, ct)
            ?? throw new ReservationGateException(404, "PARKING_SESSION_NOT_FOUND", "Parking session was not found.");
        session.AwaitCheckoutPayment(orderId);
        await work.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
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
