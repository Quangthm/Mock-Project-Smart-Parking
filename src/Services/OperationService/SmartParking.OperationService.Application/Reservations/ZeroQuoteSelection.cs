namespace SmartParking.OperationService.Application.Reservations;

public sealed record ZeroQuoteSelection(Guid SlotId, string Plate, DateTimeOffset Start, DateTimeOffset End,
    bool IsCovered, string CoverageVersion);
