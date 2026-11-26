using MediatR;
using SmartParking.PaymentService.Application.DTOs.Reservations;

namespace SmartParking.PaymentService.Application.Usecase.Reservations.Commands.CreateReservationQuote;

public sealed record CreateReservationQuoteCommand(
    CreateReservationQuoteRequestDto Request
) : IRequest<CommercialTermsResponseDto>;
