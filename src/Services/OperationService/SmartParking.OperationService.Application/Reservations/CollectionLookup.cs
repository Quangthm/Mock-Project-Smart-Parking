namespace SmartParking.OperationService.Application.Reservations;

public sealed record CollectionLookup(string State, IReadOnlyList<VerifiedCollection> Collections);
