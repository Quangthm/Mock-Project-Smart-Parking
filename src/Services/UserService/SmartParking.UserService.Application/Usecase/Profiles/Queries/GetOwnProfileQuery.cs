using MediatR;
using SmartParking.UserService.Application.Usecase.Profiles.Contracts;
using UserService.Application.Common.Interfaces.Services;

namespace SmartParking.UserService.Application.Usecase.Profiles.Queries;

/// <summary>Reads the caller's own profile.</summary>
public sealed record GetOwnProfileQuery : IRequest<OwnProfileResponse>;

public sealed class GetOwnProfileQueryHandler(ICurrentAccessService access)
    : IRequestHandler<GetOwnProfileQuery, OwnProfileResponse>
{
    public async Task<OwnProfileResponse> Handle(GetOwnProfileQuery request, CancellationToken cancellationToken)
    {
        var current = await access.RequireAsync(cancellationToken);
        return OwnProfileResponse.From(current.User, current.Role);
    }
}
