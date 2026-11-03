using MediatR;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.DTOs.Payments;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.Application.Usecase.ConfirmCashPayment;

public sealed record ConfirmCashPaymentCommand(
    Guid OrderId,
    PaymentMethod PaymentMethod = PaymentMethod.CASH,
    string? IdempotencyKey = null)
    : IRequest<PaymentTransactionStatusDto>;

public sealed class ConfirmCashPaymentCommandHandler(
    IPaymentOrderRepository orders,
    IPaymentTransactionRepository transactions,
    IUnitOfWork unitOfWork) : IRequestHandler<ConfirmCashPaymentCommand, PaymentTransactionStatusDto>
{
    public async Task<PaymentTransactionStatusDto> Handle(
        ConfirmCashPaymentCommand request,
        CancellationToken cancellationToken)
    {
        var order = await orders.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new PaymentDomainException("ORDER_NOT_FOUND", "Không tìm thấy đơn thanh toán.");

        var successful = order.Transactions.FirstOrDefault(t => t.Status == PaymentStatus.SUCCESS);
        if (successful is not null)
            return ToResponse(order, successful);

        var now = DateTimeOffset.UtcNow;
        var transaction = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            TenantId = order.TenantId,
            PaymentOrderId = order.Id,
            PaymentMethod = request.PaymentMethod,
            Amount = order.FinalAmount,
            Status = PaymentStatus.SUCCESS,
            PaidAt = now,
            CreatedAt = now
        };
        order.MarkPaid(now);
        await transactions.AddAsync(transaction, cancellationToken);
        orders.Update(order);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return ToResponse(order, transaction);
    }

    private static PaymentTransactionStatusDto ToResponse(PaymentOrder order, PaymentTransaction transaction) =>
        new(transaction.Id, order.Id, long.TryParse(order.OrderCode, out var code) ? code : 0,
            (long)transaction.Amount, transaction.PaymentMethod.ToString(), transaction.GatewayTransactionId,
            transaction.Status.ToString(), transaction.PaidAt, true);
}
