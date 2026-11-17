using MediatR;

namespace SmartParking.OperationService.Application.Reservations;

public sealed record GetPolicyRevisionsQuery(string Status, string Scope, Guid? TenantId, Guid? SiteId, int Page,
    int PageSize) : IRequest<PagedResult<PolicyRevisionListItemDto>>;

public sealed class GetPolicyRevisionsHandler(IReservationPolicyRepository policies)
    : IRequestHandler<GetPolicyRevisionsQuery, PagedResult<PolicyRevisionListItemDto>>
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ALL", "DRAFT", "SCHEDULED", "CURRENT", "SUPERSEDED"
    };

    private static readonly HashSet<string> AllowedScopes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ALL", "PLATFORM", "SITE"
    };

    public Task<PagedResult<PolicyRevisionListItemDto>> Handle(GetPolicyRevisionsQuery query, CancellationToken ct)
    {
        var status = string.IsNullOrWhiteSpace(query.Status) ? "ALL" : query.Status.Trim().ToUpperInvariant();
        var scope = string.IsNullOrWhiteSpace(query.Scope) ? "ALL" : query.Scope.Trim().ToUpperInvariant();
        var errors = new List<(string Property, string Message)>();
        if (!AllowedStatuses.Contains(status))
            errors.Add(("status", "Status must be one of ALL, DRAFT, SCHEDULED, CURRENT, SUPERSEDED."));
        if (!AllowedScopes.Contains(scope))
            errors.Add(("scope", "Scope must be one of ALL, PLATFORM, SITE."));
        if (errors.Count != 0)
            throw new RequestValidationFailedException("Request validation failed.", errors);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, 100);
        return policies.GetRevisionsAsync(status, scope, query.TenantId, query.SiteId, page, pageSize, ct);
    }
}