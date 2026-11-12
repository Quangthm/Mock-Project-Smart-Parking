using MediatR;
using SmartParking.PaymentService.Application.DTOs.Reservations;

namespace SmartParking.PaymentService.Application.Usecase.Reservations.Queries.GetReservationCollections;

public sealed record GetReservationCollectionsQuery(
    Guid OrderId,
    Guid? ReservationId = null
) : IRequest<CollectionLookupResponseDto>;
