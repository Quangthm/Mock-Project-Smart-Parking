using AutoMapper;
using SmartParking.UserService.Domain.Entities;
using UserService.Application.Usecase.Vehicles;

namespace UserService.Application.Common.MapperProfile;

public sealed class VehicleMapperProfile : Profile
{
    public VehicleMapperProfile()
    {
        CreateMap<SaveVehicleRequest, SaveOwnVehicleCommand>().ConstructUsing(request => new(
            null, request.Plate, request.VehicleType, request.ImageAssetId, request.ExpectedImageAssetId))
            .ForMember(command => command.VehicleId, options => options.Ignore());
        CreateMap<ResolveVehicleBindingRequest, ResolveVehicleBindingQuery>();
        CreateMap<RegisteredVehicle, VehicleResponse>().ConvertUsing(vehicle => new(
            vehicle.Id, vehicle.RawPlate, vehicle.CanonicalPlate, vehicle.VehicleType, vehicle.ImageAssetId,
            vehicle.ImageAssetId.HasValue ? "/api/media/images/" + vehicle.ImageAssetId.Value + "/content" : null,
            vehicle.ImageAssetId.HasValue ? "managed" : "reupload-required", vehicle.CreatedAt, vehicle.UpdatedAt));
        CreateMap<VehicleRegistrationClaim, RegistrationClaimResponse>();
    }
}
