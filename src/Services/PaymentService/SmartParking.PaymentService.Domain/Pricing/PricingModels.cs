namespace SmartParking.PaymentService.Domain.Pricing;

public enum PricingVehicleType
{
    CAR,
    MOTORCYCLE,
    OVERSIZED
}

public enum MonetaryRoundingMode
{
    HALF_UP_WHOLE_VND,
    CEILING_WHOLE_VND,
    HALF_UP_THOUSAND,
    CEILING_THOUSAND
}

public sealed class PricingDomainException : Exception
{
    public string Code { get; }
    public string? Field { get; }

    public PricingDomainException(string code, string message, string? field = null)
        : base(message)
    {
        Code = code;
        Field = field;
    }
}

public sealed record TimeBlockRule(
    string BlockCode,
    TimeOnly StartTime,
    TimeOnly EndTime,
    long RatePerHourVnd
);

public sealed record PricingSegmentBreakdown(
    int SegmentIndex,
    DateOnly LocalDate,
    DateTimeOffset SegmentStart,
    DateTimeOffset SegmentEnd,
    string BlockCode,
    decimal OverlapMinutes,
    long RatePerHourVnd,
    decimal RawChargeVnd
);
