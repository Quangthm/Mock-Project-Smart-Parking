using MediatR;

namespace SmartParking.OperationService.Application;

public sealed class OwnReadFailure(int status, string code) : Exception
{ public int Status { get; } = status; public string Code { get; } = code; }
public sealed record ReservationReader(Guid AccountId, DateTimeOffset ExpiresAt);
public interface IReservationReader { ReservationReader? Actor { get; } }
public sealed record OwnVehicle(string VehicleType, string DisplayLabel);
public sealed record OwnReservation(Guid Id, Guid SiteId, OwnVehicle Vehicle, DateTimeOffset StartAt,
    DateTimeOffset EndAt, string Status, DateTimeOffset AsOf);
public sealed record OwnReservationPage(IReadOnlyList<OwnReservation> Items, string? NextCursor, DateTimeOffset AsOf);
public sealed record OwnReservationDetail(OwnReservation Reservation, DateTimeOffset AsOf);
public sealed record ReservationCursor(Guid Account, DateTimeOffset? From, DateTimeOffset? To, int Limit,
    DateTimeOffset Start, Guid Id);
public interface IReservationReadCursors
{ string Encode(ReservationCursor cursor); ReservationCursor Decode(string cursor); }
public interface IOwnReservationStore
{
    Task<IReadOnlyList<OwnReservation>> List(Guid account, DateTimeOffset? from, DateTimeOffset? to, int limit,
        ReservationCursor? after, DateTimeOffset asOf, CancellationToken ct);
    Task<OwnReservation?> Get(Guid account, Guid id, DateTimeOffset asOf, CancellationToken ct);
}
public sealed record ListOwnReservations(DateTimeOffset? From, DateTimeOffset? To, int Limit, string? Cursor) : IRequest<OwnReservationPage>;
public sealed record GetOwnReservation(Guid Id) : IRequest<OwnReservationDetail>;
public sealed class OwnReservationHandlers(IReservationReader reader, IOwnReservationStore store,
    IReservationReadCursors cursors, TimeProvider clock)
    : IRequestHandler<ListOwnReservations, OwnReservationPage>, IRequestHandler<GetOwnReservation, OwnReservationDetail>
{
    private Guid Account()
    {
        var actor = reader.Actor;
        if (actor is null || actor.AccountId == Guid.Empty || actor.ExpiresAt <= clock.GetUtcNow()) throw new OwnReadFailure(401, "INVALID_TOKEN");
        return actor.AccountId;
    }
    public async Task<OwnReservationPage> Handle(ListOwnReservations request, CancellationToken ct)
    {
        var account = Account();
        if (request.Limit is < 1 or > 50 || request.From.HasValue != request.To.HasValue || request.To <= request.From)
            throw new OwnReadFailure(400, "VALIDATION_FAILED");
        var after = request.Cursor is null ? null : cursors.Decode(request.Cursor);
        if (after is not null && (after.Account != account || after.From != request.From || after.To != request.To || after.Limit != request.Limit))
            throw new OwnReadFailure(400, "INVALID_CURSOR");
        var asOf = clock.GetUtcNow();
        var rows = await store.List(account, request.From, request.To, request.Limit + 1, after, asOf, ct);
        var items = rows.Take(request.Limit).ToArray();
        Account();
        return new(items, rows.Count > request.Limit ? cursors.Encode(new(account, request.From, request.To, request.Limit, items[^1].StartAt, items[^1].Id)) : null, asOf);
    }
    public async Task<OwnReservationDetail> Handle(GetOwnReservation request, CancellationToken ct)
    {
        var account = Account();
        if (request.Id == Guid.Empty) throw new OwnReadFailure(400, "VALIDATION_FAILED");
        var asOf = clock.GetUtcNow();
        var item = await store.Get(account, request.Id, asOf, ct) ?? throw new OwnReadFailure(404, "RESOURCE_NOT_FOUND");
        Account();
        return new(item, asOf);
    }
}
