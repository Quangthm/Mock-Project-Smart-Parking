using AutoMapper;
using MediatR;
using UserService.Application.Common.Interfaces.Persistence;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.Common.Models.Exceptions;

namespace UserService.Application.Usecase.Vehicles;

public sealed record ListOwnVehiclesQuery : IRequest<IReadOnlyList<VehicleResponse>>;

public sealed class ListOwnVehiclesQueryHandler(IVehicleRepository vehicles, ICurrentActor actor,
    IAccountAuthorizationService authorization, IMapper mapper)
    : IRequestHandler<ListOwnVehiclesQuery, IReadOnlyList<VehicleResponse>>
{
    public async Task<IReadOnlyList<VehicleResponse>> Handle(ListOwnVehiclesQuery request, CancellationToken ct)
    {
        var current = await authorization.RequireDriverAsync(actor.UserId ?? throw AuthException.InvalidToken(), false, ct);
        return mapper.Map<VehicleResponse[]>(await vehicles.ListAsync(current.Account.Id!.Value, ct));
    }
}
