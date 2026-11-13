using MediatR;
using SmartParking.PaymentService.Application.DTOs.Reservations;

namespace SmartParking.PaymentService.Application.Usecase.Reservations.Commands.EnsureReservationDepositOrder;

public sealed record EnsureReservationDepositOrderCommand(
    EnsureReservationDepositOrderRequestDto Request
) : IRequest<EnsureReservationDepositOrderResponseDto>;
