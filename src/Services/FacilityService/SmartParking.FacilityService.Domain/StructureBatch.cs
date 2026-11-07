namespace SmartParking.FacilityService.Domain;

// One selected resource in a same-lot batch. Every affected resource is explicit: a parent is never
// implicitly removed with its children, so there is no descendant cascade.
public sealed record StructureBatchItem(Guid ResourceId, string Action, DateTime? ExpectedUpdatedAt = null);
public sealed record StructureBatchFailure(Guid ResourceId, string Code, string Message);

// All-or-nothing: when any item fails no item is applied, and the failure list names only the
// selected resources that failed. A foreign or missing id is reported as NOT_FOUND without details.
public sealed class StructureBatchException(IReadOnlyList<StructureBatchFailure> failures)
    : StructureException("STRUCTURE_CONFLICT", "One or more batch items failed; nothing was applied.")
{
    public IReadOnlyList<StructureBatchFailure> Failures { get; } = failures;
}
