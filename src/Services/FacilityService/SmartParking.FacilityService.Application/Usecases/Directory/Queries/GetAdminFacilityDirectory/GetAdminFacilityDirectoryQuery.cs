using MediatR;
using SmartParking.FacilityService.Application.DTOs;

namespace SmartParking.FacilityService.Application.Usecases.Directory.Queries.GetAdminFacilityDirectory;

public sealed record GetAdminFacilityDirectoryQuery : IRequest<AdminFacilityDirectoryDto>;
