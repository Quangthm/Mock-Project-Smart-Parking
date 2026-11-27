using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutoMapper;
using FluentValidation;
using MediatR;
using SmartParking.Contracts.AccountAccess;
using SmartParking.OperationService.Application.Common.Interfaces.Persistence;
using SmartParking.OperationService.Application.Common.Models.Exceptions;
using SmartParking.OperationService.Domain.Reservations;

namespace SmartParking.OperationService.Application.Reservations;

public sealed class AdmissionMapperProfile : Profile
{
    public AdmissionMapperProfile() => CreateMap<ReservationRequest, ReservationReceipt>()
        .ForCtorParam(nameof(ReservationReceipt.RequestId), opt => opt.MapFrom(x => x.Id))
        .ForCtorParam(nameof(ReservationReceipt.ReservationId), opt => opt.MapFrom(x => x.ReservationId))
        .ForCtorParam(nameof(ReservationReceipt.ContractVersion), opt => opt.MapFrom(x => x.AccountCheck == null ? 2 : 3));
}
