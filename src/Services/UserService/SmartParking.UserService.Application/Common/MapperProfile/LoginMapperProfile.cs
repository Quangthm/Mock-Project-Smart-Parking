using AutoMapper;
using UserService.Application.DTOs;
using UserService.Application.Usecase.Login;

namespace UserService.Application.Common.MapperProfile
{
    /// <summary>
    /// Login AutoMapper Profile.
    /// </summary>
    /// <seealso cref="AutoMapper.Profile" />
    public class LoginMapperProfile : Profile
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LoginMapperProfile" /> class.
        /// </summary>
        public LoginMapperProfile()
        {
            CreateMap<LoginCommand, LoginDto>()
                .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email))
                .ForMember(dest => dest.Password, opt => opt.MapFrom(src => src.Password))
                .ReverseMap();
        }
    }
}
