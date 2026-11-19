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

internal static class AdmissionMessage
{
    public static AccountAccessRequestV1 For(ReservationRequest request) => new(request.Id, request.AttemptId,
        request.AttemptNumber, request.UserId, request.AccountId, "driver", request.Operation,
        request.AdmittedAt, request.AttemptDeadline, request.OverallDeadline);
}
