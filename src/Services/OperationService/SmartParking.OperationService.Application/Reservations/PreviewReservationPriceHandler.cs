using MediatR;
using SmartParking.OperationService.Application.Common.Models.Exceptions;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class PreviewReservationPriceHandler(ICurrentActor actor, IReservationCommercialOwner commercial)
    : IRequestHandler<PreviewReservationPriceQuery, PricePreview>
{
    public async Task<PricePreview> Handle(PreviewReservationPriceQuery query, CancellationToken ct)
    {
        var (_, account) = AdmissionActor.RequireDriver(actor);
        var result = await commercial.PreviewAsync(account, query.Intent with
        { Start = query.Intent.Start.ToUniversalTime(), End = query.Intent.End.ToUniversalTime() }, ct);
        if (result.BookedAmount < 0 || result.DepositAmount < 0 || result.Currency != "VND" || string.IsNullOrWhiteSpace(result.PricingVersion))
            throw BookingContracts.InvalidOwner();
        return result;
    }
}
