namespace SmartParking.PaymentService.Domain.Pricing;

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
