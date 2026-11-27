namespace SmartParking.OperationService.Application.Reservations;

public sealed record ReviewedOperationalPolicy(Guid PlatformRevisionId, int PlatformVersion,
    Guid SiteRevisionId, int SiteVersion, IReadOnlyDictionary<string, int> Values,
    bool ArrivalLateToleranceEnabled, string CancellationTimezone);
