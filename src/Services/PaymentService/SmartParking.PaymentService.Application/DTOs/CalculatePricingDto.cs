using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Application.DTOs;

public sealed record CalculatePricingRequestDto(
    Guid TenantId,
    Guid SiteId,
    string VehicleType,
    DateTimeOffset EntryTime,
    DateTimeOffset ExitTime,
    Guid? SessionId = null,
    Guid? ReservationId = null,
    PricingPolicySnapshot? PolicyOverride = null
);

public sealed record PricingSegmentBreakdownDto(
    int SegmentIndex,
    DateOnly LocalDate,
    DateTimeOffset SegmentStart,
    DateTimeOffset SegmentEnd,
    string BlockCode,
    decimal OverlapMinutes,
    long RatePerHourVnd,
    decimal RawChargeVnd
);

public sealed record CalculatePricingResponseDto(
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
    string RoundingMode,
    Guid AppliedTariffId,
    string AppliedTariffName,
    int AppliedPolicyVersion,
    IReadOnlyList<PricingSegmentBreakdownDto> Segments
);
