using MediatR;
using SmartParking.PaymentService.Application.DTOs.Reservations;

namespace SmartParking.PaymentService.Application.Usecase.Reservations.Commands.ReconcileReservation;

public sealed record ReconcileReservationCollectionCommand(
    ReconcileReservationRequestDto Request
) : IRequest<ReconcileReservationResponseDto>;
