using MediatR;
using SmartParking.FacilityService.Application;

namespace SmartParking.FacilityService.Application.Usecases.Internal.Queries.ValidateSites;

public sealed record ValidateSitesQuery(SiteValidation Body) : IRequest<IReadOnlyList<ValidatedSite>>;
