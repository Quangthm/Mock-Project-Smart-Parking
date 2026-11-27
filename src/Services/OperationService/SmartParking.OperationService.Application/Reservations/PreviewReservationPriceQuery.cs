using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record PreviewReservationPriceQuery(ReservationPricePreview Intent) : IRequest<PricePreview>;
