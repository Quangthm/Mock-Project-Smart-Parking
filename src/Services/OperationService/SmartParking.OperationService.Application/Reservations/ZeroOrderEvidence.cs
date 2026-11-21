namespace SmartParking.OperationService.Application.Reservations;

/// <summary>Payment has durably accepted the exact zero order and recorded its receipt or issuance obligation.</summary>
public sealed record ZeroOrderEvidence(ZeroOrderBinding Binding, Guid OrderId, Guid ReceiptId);
