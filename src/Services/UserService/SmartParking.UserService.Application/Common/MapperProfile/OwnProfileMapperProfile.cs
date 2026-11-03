using AutoMapper;
using SmartParking.UserService.Application.Usecase.Profiles.Commands;
using SmartParking.UserService.Application.Usecase.Profiles.Contracts;

namespace UserService.Application.Common.MapperProfile;

public sealed class OwnProfileMapperProfile : Profile
{
    public OwnProfileMapperProfile() => CreateMap<UpdateOwnProfileRequest, UpdateOwnProfileCommand>()
        .ConstructUsing(request => new UpdateOwnProfileCommand(request.FullName ?? string.Empty));
}
