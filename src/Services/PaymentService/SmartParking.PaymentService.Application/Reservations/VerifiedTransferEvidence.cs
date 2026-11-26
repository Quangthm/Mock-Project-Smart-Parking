namespace SmartParking.PaymentService.Application.Reservations;

public sealed record VerifiedTransferEvidence(int Version, Guid EventId, Guid OperationId, Guid PaymentTransactionId,
    string PaymentMethod, string Provider, string ProviderReference, DateTimeOffset ProviderVerifiedAt, DateTimeOffset OccurredAt);
