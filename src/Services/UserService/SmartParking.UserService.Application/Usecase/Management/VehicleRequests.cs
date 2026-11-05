using MediatR;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Common.Interfaces.Services;
using UserService.Application.DTOs;

namespace UserService.Application.Usecase.Management;

public sealed record ListOwnVehiclesQuery : IRequest<IReadOnlyList<RegisteredVehicle>>;
public sealed record SaveOwnVehicleCommand(Guid? VehicleId, VehicleDto Body) : IRequest<RegisteredVehicle>;
public sealed class VehicleRequestHandler(IVehicleRegistration vehicles, ICurrentAccessService current,
    IAccountAuthorizationService authorization)
    : IRequestHandler<ListOwnVehiclesQuery, IReadOnlyList<RegisteredVehicle>>,
      IRequestHandler<SaveOwnVehicleCommand, RegisteredVehicle>
{
    private async Task<Guid> RequireDriverAsync(CancellationToken ct)
    {
        var id = (await current.RequireAsync(ct)).User.Id!.Value;
        await authorization.RequireDriverAsync(id, false, ct);
        return id;
    }
    public async Task<IReadOnlyList<RegisteredVehicle>> Handle(ListOwnVehiclesQuery request, CancellationToken ct) =>
        await vehicles.ListAsync(await RequireDriverAsync(ct), ct);
    public async Task<RegisteredVehicle> Handle(SaveOwnVehicleCommand request, CancellationToken ct) =>
        await vehicles.SaveAsync(await RequireDriverAsync(ct), request.VehicleId, request.Body, ct);
}
