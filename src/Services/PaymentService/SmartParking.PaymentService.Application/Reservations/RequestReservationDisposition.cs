using SmartParking.Contracts.ReservationPayments;
using MediatR;
namespace SmartParking.PaymentService.Application.Reservations;

public sealed record RequestReservationDisposition(RequestReservationDispositionV1 Evidence) : IRequest;
