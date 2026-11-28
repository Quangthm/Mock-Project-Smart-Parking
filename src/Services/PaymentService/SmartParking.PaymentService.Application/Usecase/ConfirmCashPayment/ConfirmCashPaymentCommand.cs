using MediatR;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.DTOs.Payments;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;
using SmartParking.PaymentService.Application.Common.Interfaces.Services;

namespace SmartParking.PaymentService.Application.Usecase.ConfirmCashPayment;

public sealed record ConfirmCashPaymentCommand(
    Guid OrderId,
    PaymentMethod PaymentMethod = PaymentMethod.CASH,
    string? IdempotencyKey = null)
    : IRequest<PaymentTransactionStatusDto>;
