using MediatR;
using SmartParking.OperationService.Application.Reservations;
using SmartParking.OperationService.Application.Common.Models.Exceptions;

namespace SmartParking.OperationService.Application.Discovery;

public sealed class ReadHistoricalLocationHandler(ICurrentActor actor, IHistoricalBookingSiteReader bookings,
    IHistoricalSiteLocationOwner facility) : IRequestHandler<ReadHistoricalLocationQuery, HistoricalLocation?>
{
    public async Task<HistoricalLocation?> Handle(ReadHistoricalLocationQuery query, CancellationToken ct)
    {
        var (_, accountId) = AdmissionActor.RequireDriver(actor);
        var booking = await bookings.LatestConfirmedAsync(accountId, ct);
        if (booking is null) return null;
        var site = await facility.ReadPublishedAsync(booking.SiteId, ct);
        if (site is null) return null;
        if (site.SiteId != booking.SiteId || site.SiteName is null || site.SiteName.Length is 0 or > 255 ||
            site.Latitude is < -90 or > 90 || site.Longitude is < -180 or > 180)
            throw new ReservationGateException(503, "OWNER_CONTRACT_INVALID", "Facility site location is inconsistent.");
        return new(site.SiteId, site.SiteName, site.Latitude, site.Longitude, booking.ConfirmedAt);
    }
}
