using MediatR;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.DTOs.Payments;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.Application.Usecase.CreatePaymentOrder;

public sealed record CreatePaymentOrderCommand(CreatePaymentOrderRequestDto Request)
    : IRequest<PaymentOrderResponseDto>;

public sealed record PaymentOrderResponseDto(
    Guid OrderId,
    string OrderCode,
    decimal Amount,
    string Status,
    DateTimeOffset ExpiresAt);

public sealed class CreatePaymentOrderCommandHandler(
    IPaymentOrderRepository orders,
    IUnitOfWork unitOfWork) : IRequestHandler<CreatePaymentOrderCommand, PaymentOrderResponseDto>
{
    public async Task<PaymentOrderResponseDto> Handle(
        CreatePaymentOrderCommand command,
        CancellationToken cancellationToken)
    {
        var request = command.Request;
        if (request.Amount < 0)
            throw new PaymentDomainException("INVALID_AMOUNT", "Số tiền thanh toán không hợp lệ.");

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await orders.GetByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
            if (existing is not null)
                return ToResponse(existing);
        }

        var now = DateTimeOffset.UtcNow;
        var order = new PaymentOrder
        {
            Id = Guid.NewGuid(),
            TenantId = request.TenantId,
            SiteId = request.SiteId,
            AccountId = request.AccountId,
            OrderCode = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
            IdempotencyKey = request.IdempotencyKey,
            ReferenceType = request.ReferenceType,
            ReferenceId = request.ReferenceId,
            Amount = request.Amount,
            FinalAmount = request.Amount,
            ExpiresAt = now.AddMinutes(15),
            CreatedAt = now,
            UpdatedAt = now
        };
        await orders.AddAsync(order, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(order);
    }

    private static PaymentOrderResponseDto ToResponse(PaymentOrder order) =>
        new(order.Id, order.OrderCode, order.FinalAmount, order.Status.ToString(),
            order.ExpiresAt ?? order.CreatedAt.AddMinutes(15));
}
