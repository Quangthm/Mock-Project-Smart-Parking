using MediatR;
namespace SmartParking.PaymentService.Application.Reservations;

public sealed record ReadReservationCheckout(Guid AccountId, Guid? ReservationId, Guid? OrderId) : IRequest<ReservationCheckoutState>;
