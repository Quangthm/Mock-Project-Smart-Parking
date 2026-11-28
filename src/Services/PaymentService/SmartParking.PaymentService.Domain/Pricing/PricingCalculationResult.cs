namespace SmartParking.PaymentService.Domain.Pricing;

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
