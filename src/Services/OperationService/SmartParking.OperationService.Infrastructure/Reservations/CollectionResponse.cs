namespace SmartParking.OperationService.Infrastructure.Reservations;

internal sealed record CollectionResponse(string State, IReadOnlyList<CollectionItem>? Collections);
