using MediatR;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.DTOs;

namespace UserService.Application.Usecase.Management;

public sealed record ListOwnerApplicationsQuery : IRequest<IReadOnlyList<OwnerApplicationDto>>;
public sealed record GetOwnerApplicationQuery(Guid ApplicationId) : IRequest<OwnerApplicationDto?>;
public sealed record ReviewOwnerApplicationCommand(Guid ApplicationId, ReviewOwnerDto Body) : IRequest<OwnerApplicationDto>;

public sealed class OwnerApplicationRequestHandler(IOwnerRegistrationService registrations, ICurrentAccessService current,
    IAccountAuthorizationService authorization)
    : IRequestHandler<ListOwnerApplicationsQuery, IReadOnlyList<OwnerApplicationDto>>,
      IRequestHandler<GetOwnerApplicationQuery, OwnerApplicationDto?>, IRequestHandler<ReviewOwnerApplicationCommand, OwnerApplicationDto>
{
    private async Task<Guid> RequireReviewerAsync(CancellationToken ct)
    {
        var id = (await current.RequireAsync(ct)).User.Id!.Value;
        await authorization.RequireOwnerReviewAsync(id, false, ct);
        return id;
    }
    public async Task<IReadOnlyList<OwnerApplicationDto>> Handle(ListOwnerApplicationsQuery request, CancellationToken ct) =>
        await registrations.ListAsync(await RequireReviewerAsync(ct), ct);
    public async Task<OwnerApplicationDto?> Handle(GetOwnerApplicationQuery request, CancellationToken ct) =>
        (await registrations.ListAsync(await RequireReviewerAsync(ct), ct))
            .SingleOrDefault(a => a.Id == request.ApplicationId);
    public async Task<OwnerApplicationDto> Handle(ReviewOwnerApplicationCommand request, CancellationToken ct) =>
        await registrations.ReviewAsync(await RequireReviewerAsync(ct), request.ApplicationId, request.Body, ct);
}
