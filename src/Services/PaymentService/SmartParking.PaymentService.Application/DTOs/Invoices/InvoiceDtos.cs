namespace SmartParking.PaymentService.Application.DTOs.Invoices;

public sealed record PrepareParkingFeeOrderRequestDto(
    Guid TenantId,
    Guid SiteId,
    Guid SessionId,
    string VehicleType,
    string PlateNumber,
    DateTimeOffset EntryTime,
    DateTimeOffset PricingCutoffAt,
    Guid? AccountId,
    decimal? PenaltyAmount,
    string? PenaltyReason,
    string IdempotencyKey
);

public sealed record PrepareParkingFeeOrderResponseDto(
    Guid OrderId,
    string OrderCode,
    Guid SessionId,
    string Currency,
    decimal BaseParkingFee,
    decimal PenaltyFee,
    decimal FinalAmount,
    string Status,
    Guid TariffId,
    int TariffVersion,
    int TotalParkedMinutes,
    DateTimeOffset? ExpiresAt,
    DateTimeOffset CreatedAt
);

public sealed record CustomerInvoiceResponseDto(
    InvoiceIdentityDto InvoiceIdentity,
    CustomerReferenceDto CustomerReference,
    ParkingSessionReferenceDto ParkingSessionReference,
    TimingDto Timing,
    TariffPolicyDto TariffPolicy,
    FinancialSummaryDto FinancialSummary,
    FinancialStatusDto FinancialStatus,
    IReadOnlyList<InvoiceLineItemDto> LineItems
);

public sealed record InvoiceIdentityDto(
    Guid InvoiceId,
    string InvoiceNumber,
    Guid PaymentOrderId,
    string OrderCode,
    DateTimeOffset IssuedAt
);

public sealed record CustomerReferenceDto(
    Guid? AccountId,
    string CustomerType,
    string? DisplayName
);

public sealed record ParkingSessionReferenceDto(
    Guid SessionId,
    string ReferenceType,
    Guid SiteId,
    string SiteName,
    string VehicleType,
    string PlateNumber
);

public sealed record TimingDto(
    DateTimeOffset EntryTime,
    DateTimeOffset PricingCutoffAt,
    int TotalDurationMinutes,
    int BillableDurationMinutes
);

public sealed record TariffPolicyDto(
    Guid TariffId,
    int TariffVersion,
    string PolicyName,
    int FreeParkingGraceMinutes
);

public sealed record FinancialSummaryDto(
    decimal SubtotalParkingFee,
    decimal TotalPenaltyFee,
    decimal DiscountAmount,
    decimal FinalTotalAmount,
    string Currency
);

public sealed record FinancialStatusDto(
    string Status,
    bool IsSettled,
    DateTimeOffset? PaidAt,
    string? PaymentMethod,
    string? TransactionReference,
    string? GatewayTransactionId
);

public sealed record InvoiceLineItemDto(
    string ItemType,
    string Description,
    DateTimeOffset? StartTime,
    DateTimeOffset? EndTime,
    int DurationMinutes,
    int CoveredMinutes,
    int ChargeableMinutes,
    decimal UnitRate,
    string Unit,
    decimal Amount
);

public sealed record CashPaymentRequestDto(
    decimal? AmountReceived = null,
    string? Notes = null,
    string? IdempotencyKey = null
);
