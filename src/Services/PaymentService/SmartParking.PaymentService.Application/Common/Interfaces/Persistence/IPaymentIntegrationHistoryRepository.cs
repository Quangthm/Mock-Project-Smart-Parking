namespace SmartParking.PaymentService.Application.Common.Interfaces.Persistence;

/// <summary>
/// Persisted record of an accepted payment integration configuration revision.
/// </summary>
public sealed record PaymentIntegrationRevision(
    Guid PolicyId,
    int RevisionNumber,
    string PolicyValuesJson,
    Guid CreatedBy,
    DateTimeOffset CreatedAt);

/// <summary>
/// Application port for the persisted payment integration configuration history.
/// Implementations live in the persistence layer and reuse the existing
/// <c>policy_revisions</c> table (no schema change).
/// </summary>
public interface IPaymentIntegrationHistoryRepository
{
    /// <summary>Returns the latest persisted revision for the given policy, or null.</summary>
    Task<PaymentIntegrationRevision?> GetLatestAsync(Guid policyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a new revision with a monotonically increasing revision number for the
    /// given policy id.
    /// </summary>
    Task AppendAsync(
        Guid policyId,
        string policyValuesJson,
        Guid createdBy,
        DateTimeOffset createdAt,
        CancellationToken cancellationToken = default);
}
