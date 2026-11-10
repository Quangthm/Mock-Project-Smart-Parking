using AutoMapper;
using MediatR;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;

namespace UserService.Application.Usecase.Vehicles;

public sealed record ListRegistrationClaimsQuery : IRequest<IReadOnlyList<RegistrationClaimResponse>>;

public sealed class ListRegistrationClaimsQueryHandler(IRegistrationClaimRepository claims, ICurrentActor actor,
    IAccountAuthorizationService authorization, IMapper mapper)
    : IRequestHandler<ListRegistrationClaimsQuery, IReadOnlyList<RegistrationClaimResponse>>
{
    public async Task<IReadOnlyList<RegistrationClaimResponse>> Handle(ListRegistrationClaimsQuery request, CancellationToken ct)
    {
        var current = await authorization.RequireDriverAsync(actor.UserId ?? throw AuthException.InvalidToken(), false, ct);
        return mapper.Map<RegistrationClaimResponse[]>(await claims.ListPendingAsync(current.Account.Id!.Value, ct));
    }
}
