namespace SmartParking.PaymentService.Application.Common.Interfaces.Ports;

public sealed record ReviewedPayOsTransfer(
    long OrderCode,
    string? PaymentLinkId,
    string? BankReference,
    long AmountVnd,
    DateTimeOffset? OccurredAt,
    string? EvidenceFailure = null);
