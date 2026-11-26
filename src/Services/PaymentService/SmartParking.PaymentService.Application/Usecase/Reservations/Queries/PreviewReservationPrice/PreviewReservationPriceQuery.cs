using MediatR;
using SmartParking.PaymentService.Application.DTOs.Reservations;

namespace SmartParking.PaymentService.Application.Usecase.Reservations.Queries.PreviewReservationPrice;

public sealed record PreviewReservationPriceQuery(
    ReservationPricePreviewRequestDto Request
) : IRequest<ReservationPricePreviewResponseDto>;
