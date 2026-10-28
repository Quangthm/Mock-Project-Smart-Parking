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

public sealed record PricingPolicySnapshot(
    Guid TariffId,
    string TariffName,
    int PolicyVersion,
    PricingVehicleType VehicleType,
    int FreeParkingGraceMinutes,
    IReadOnlyList<TimeBlockRule> TimeBlocks,
    string TimeZoneId = "Asia/Ho_Chi_Minh",
    MonetaryRoundingMode RoundingMode = MonetaryRoundingMode.HALF_UP_THOUSAND
);

public sealed record PricingCalculationRequest(
    Guid TenantId,
    Guid SiteId,
    PricingVehicleType VehicleType,
    DateTimeOffset EntryTime,
    DateTimeOffset ExitTime,
    PricingPolicySnapshot Policy,
    Guid? SessionId = null,
    Guid? ReservationId = null
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

public sealed record PricingCalculationResult(
    string Currency,
    DateTimeOffset LocalEntryTime,
    DateTimeOffset LocalExitTime,
    string TimeZoneId,
    decimal TotalParkedMinutes,
    int InvoiceRoundedMinutes,
    decimal BilledMinutes,
    int FreeParkingGraceMinutes,
    bool IsFreeStay,
    decimal UnroundedTotalVnd,
    long FinalAmountVnd,
    MonetaryRoundingMode RoundingMode,
    Guid AppliedTariffId,
    string AppliedTariffName,
    int AppliedPolicyVersion,
    IReadOnlyList<PricingSegmentBreakdown> Segments
);
