namespace SmartParking.PaymentService.Application.Reservations;

public sealed record ZeroQuoteSelection(Guid SlotId, string Plate, DateTimeOffset Start, DateTimeOffset End,
    bool IsCovered, string CoverageVersion)
{
    public static ZeroQuoteSelection From(ReviewedQuoteBinding b) =>
        new(b.RequestedSlotId, b.CanonicalPlate, b.Start, b.End, b.Coverage.IsCovered, b.Coverage.Version);
}
