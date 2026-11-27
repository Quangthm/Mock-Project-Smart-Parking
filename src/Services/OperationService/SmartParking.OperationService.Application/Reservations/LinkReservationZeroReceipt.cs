using SmartParking.Contracts.ReservationPayments;
using MediatR;
namespace SmartParking.OperationService.Application.Reservations;

public sealed record LinkReservationZeroReceipt(ReservationZeroReceiptIssuedV1 Evidence) : IRequest;
