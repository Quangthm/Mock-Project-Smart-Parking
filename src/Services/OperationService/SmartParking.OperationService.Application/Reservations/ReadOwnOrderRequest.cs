using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record ReadOwnOrderRequest(Guid OrderId) : IRequest<Guid?>;
