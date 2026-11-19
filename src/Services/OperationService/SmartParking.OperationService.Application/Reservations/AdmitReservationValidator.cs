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

public sealed class AdmitReservationValidator : AbstractValidator<AdmitReservationCommand>
{
    public AdmitReservationValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty();
        RuleFor(x => x.Intent).NotNull();
        When(x => x.Intent is not null, () =>
        {
            RuleFor(x => x.Intent.VehicleId).NotEmpty();
            RuleFor(x => x.Intent.SiteId).NotEmpty();
            RuleFor(x => x.Intent.RequestedSlotId).NotEmpty();
            RuleFor(x => x.Intent.TermsId).NotEmpty();
            RuleFor(x => x.Intent.TermsVersion).NotEmpty().MaximumLength(256);
            RuleFor(x => x.Intent.Mode).Equal("SPECIFIC_SLOT");
            RuleFor(x => x.Intent.PaymentMethod).NotEmpty().MaximumLength(32);
            RuleFor(x => x.Intent.End).GreaterThan(x => x.Intent.Start);
        });
    }
}
