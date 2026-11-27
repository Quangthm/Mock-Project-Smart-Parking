using MediatR;
using SmartParking.OperationService.Application.Reservations;

namespace SmartParking.OperationService.Tests;

internal sealed class AdmissionHttpDispatch : IRequestHandler<AdmitQuoteReservation, ReservationReceipt>,
    IRequestHandler<PreviewReservationPriceQuery, PricePreview>
{
    public Guid RequestId { get; } = Guid.NewGuid();
    public int CreationCalls { get; private set; }
    public AdmitQuoteReservation? LastCommand { get; private set; }

    public Task<ReservationReceipt> Handle(AdmitQuoteReservation command, CancellationToken ct)
    {
        CreationCalls++;
        LastCommand = command;
        return Task.FromResult(new ReservationReceipt(RequestId, "CHECKING", null, null, 3));
    }

    public Task<PricePreview> Handle(PreviewReservationPriceQuery query, CancellationToken ct) =>
        Task.FromResult(new PricePreview(10_000, 10_000, "VND", "test-pricing", false));
}
