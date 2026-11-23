using MassTransit;

namespace SmartParking.OperationService.Infrastructure.Messaging;

[MessageUrn("SmartParking.Contracts.Facility:LayoutInvalidatedV1")]
public sealed record FacilityLayoutInvalidatedMessage(
    Guid EventId,
    int SchemaVersion,
    DateTimeOffset OccurredAt,
    Guid TenantId,
    Guid SiteId,
    string[] AffectedResources);
