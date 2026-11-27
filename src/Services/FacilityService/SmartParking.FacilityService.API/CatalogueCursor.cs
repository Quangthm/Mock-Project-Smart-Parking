using SmartParking.FacilityService.Application;

namespace SmartParking.FacilityService.API;

internal sealed record CatalogueCursor(CatalogueFilter Filter, CataloguePosition Position, DateTimeOffset ExpiresAt);
