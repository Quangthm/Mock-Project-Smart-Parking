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
    string IdempotencyKey,
    decimal? DepositAmount = 0
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
    DateTimeOffset CreatedAt,
    decimal DepositDeducted = 0,
    decimal SurplusRefund = 0
);

public sealed record CustomerInvoiceResponseDto(
    InvoiceIdentityDto InvoiceIdentity,
    CustomerReferenceDto CustomerReference,
    ParkingSessionReferenceDto ParkingSessionReference,
    TimingDto Timing,
    TariffPolicyDto TariffPolicy,
    FinancialSummaryDto FinancialSummary,
    FinancialStatusDto FinancialStatus,
    IReadOnlyList<InvoiceLineItemDto> LineItems,
    string? PdfUrl = null
);

public sealed record InvoiceIdentityDto(
    Guid InvoiceId,
    string InvoiceNumber,
    Guid PaymentOrderId,
    string OrderCode,
    DateTimeOffset IssuedAt,
    string? PdfUrl = null
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
    string Currency,
    decimal DepositDeducted = 0,
    decimal SurplusRefund = 0
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

public sealed record InvoicePdfResultDto(
    byte[] Content,
    string ContentType,
    string FileName
);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize,
    int TotalPages
);

public sealed record DriverInvoiceHistoryFilterDto(
    string? Status = null,
    DateTimeOffset? FromDate = null,
    DateTimeOffset? ToDate = null,
    int PageNumber = 1,
    int PageSize = 10
);

