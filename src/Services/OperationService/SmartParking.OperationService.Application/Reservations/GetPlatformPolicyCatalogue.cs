using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record GetPlatformPolicyCatalogueQuery : IRequest<PlatformPolicyCatalogueDto>;

public sealed class GetPlatformPolicyCatalogueHandler(IReservationPolicyRepository policies)
    : IRequestHandler<GetPlatformPolicyCatalogueQuery, PlatformPolicyCatalogueDto>
{
    public Task<PlatformPolicyCatalogueDto> Handle(GetPlatformPolicyCatalogueQuery query, CancellationToken ct) =>
        policies.GetCatalogueAsync(ct);
}