using SmartParking.Contracts.ReservationPayments;
using MediatR;
namespace SmartParking.PaymentService.Application.Usecase.Reservations.Commands.EnsureReservationDepositOrder;

public sealed record EnsureReservationDepositOrderCommand(EnsureReservationOrderV1 Request) : IRequest;
