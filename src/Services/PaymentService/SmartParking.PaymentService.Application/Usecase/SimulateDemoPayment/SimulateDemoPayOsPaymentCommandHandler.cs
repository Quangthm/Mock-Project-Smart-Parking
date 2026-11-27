using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.Common.Interfaces.Ports;
using SmartParking.PaymentService.Application.DTOs.Payments;
using SmartParking.PaymentService.Application.Usecase.ProcessWebhook;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.Application.Usecase.SimulateDemoPayment;

public sealed class SimulateDemoPayOsPaymentCommandHandler
    : IRequestHandler<SimulateDemoPayOsPaymentCommand, PaymentTransactionStatusDto>
{
    private readonly IPaymentOrderRepository _orderRepository;
    private readonly IPaymentTransactionRepository _transactionRepository;
    private readonly IPayOsWebhookSimulatorPort _webhookSimulatorPort;
    private readonly ICurrentActor _currentActor;
    private readonly IMediator _mediator;
    private readonly ILogger<SimulateDemoPayOsPaymentCommandHandler> _logger;

    public SimulateDemoPayOsPaymentCommandHandler(
        IPaymentOrderRepository orderRepository,
        IPaymentTransactionRepository transactionRepository,
        IPayOsWebhookSimulatorPort webhookSimulatorPort,
        ICurrentActor currentActor,
        IMediator mediator,
        ILogger<SimulateDemoPayOsPaymentCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _transactionRepository = transactionRepository;
        _webhookSimulatorPort = webhookSimulatorPort;
        _currentActor = currentActor;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<PaymentTransactionStatusDto> Handle(
        SimulateDemoPayOsPaymentCommand request,
        CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new PaymentDomainException(
                PaymentDomainException.OrderNotFound,
                $"Payment order {request.OrderId} was not found.");

        var isPrivilegedActor = _currentActor.IsInternalService ||
            string.Equals(_currentActor.Role, "OPERATOR", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(_currentActor.Role, "ADMIN", StringComparison.OrdinalIgnoreCase);

        if (!isPrivilegedActor)
        {
            if (!_currentActor.IsAuthenticated ||
                !order.AccountId.HasValue ||
                _currentActor.AccountId != order.AccountId.Value)
            {
                throw new PaymentDomainException(
                    "FORBIDDEN_ORDER_ACCESS",
                    "You are not authorized to simulate payment for this payment order.");
            }
        }

        if (order.Status == PaymentOrderStatus.PAID)
        {
            var existingSettled = order.Transactions
                .OrderByDescending(t => t.PaidAt ?? t.CreatedAt)
                .FirstOrDefault(t => t.Status == PaymentStatus.SUCCESS)
                ?? (await _transactionRepository.GetByOrderIdAsync(order.Id, cancellationToken))
                    .OrderByDescending(t => t.PaidAt ?? t.CreatedAt)
                    .FirstOrDefault(t => t.Status == PaymentStatus.SUCCESS);

            if (existingSettled != null)
            {
                return ToStatusDto(order, existingSettled);
            }
        }

        if (order.Status == PaymentOrderStatus.CANCELLED)
        {
            throw new PaymentDomainException(
                "ORDER_CANCELLED",
                "This payment order has been cancelled.");
        }

        var now = DateTimeOffset.UtcNow;
        if (order.IsExpired(now))
        {
            throw new PaymentDomainException(
                PaymentDomainException.OrderExpired,
                "This payment order has expired.");
        }

        if (order.FinalAmount <= 0)
        {
            throw new PaymentDomainException(
                "ZERO_AMOUNT_ORDER",
                "This order has no amount due, so a PayOS payment cannot be simulated.");
        }

        if (!long.TryParse(order.OrderCode, out var orderCodeNumber) || orderCodeNumber <= 0)
        {
            throw new PaymentDomainException(
                "INVALID_ORDER_CODE",
                $"Payment order {order.Id} does not have a valid numeric PayOS order code.");
        }

        var pendingTx = order.Transactions.FirstOrDefault(t => t.Status == PaymentStatus.PENDING);

        var simulatedWebhook = _webhookSimulatorPort.BuildSignedSuccessWebhook(
            new DemoPayOsWebhookRequest(
                OrderId: order.Id,
                OrderCode: orderCodeNumber,
                AmountVnd: checked((long)order.FinalAmount),
                ExistingGatewayTransactionId: pendingTx?.GatewayTransactionId,
                OccurredAt: now));

        var webhookResult = await _mediator.Send(
            new ProcessPayOsWebhookCommand(simulatedWebhook.RawPayload, simulatedWebhook.Signature),
            cancellationToken);

        if (!webhookResult.Success)
        {
            throw new PaymentDomainException(
                webhookResult.ErrorCode ?? "DEMO_WEBHOOK_FAILED",
                webhookResult.Message);
        }

        var transactions = await _transactionRepository.GetByOrderIdAsync(order.Id, cancellationToken);
        var settledTx = transactions
            .OrderByDescending(t => t.PaidAt ?? t.CreatedAt)
            .FirstOrDefault(t => t.Status == PaymentStatus.SUCCESS)
            ?? order.Transactions
                .OrderByDescending(t => t.PaidAt ?? t.CreatedAt)
                .FirstOrDefault(t => t.Status == PaymentStatus.SUCCESS);

        if (settledTx == null)
        {
            throw new PaymentDomainException(
                "DEMO_PAYMENT_NOT_SETTLED",
                "Simulated PayOS webhook succeeded but no settled payment transaction was found.");
        }

        _logger.LogInformation(
            "Completed simulated demo PayOS webhook payment for OrderId {OrderId}, OrderCode {OrderCode}, TxId {TxId}.",
            order.Id,
            order.OrderCode,
            settledTx.Id);

        return ToStatusDto(order, settledTx);
    }

    private static PaymentTransactionStatusDto ToStatusDto(PaymentOrder order, PaymentTransaction transaction) =>
        new(
            TransactionId: transaction.Id,
            OrderId: order.Id,
            OrderCode: long.TryParse(order.OrderCode, out var code) ? code : 0,
            Amount: (long)transaction.Amount,
            PaymentMethod: transaction.PaymentMethod.ToString(),
            GatewayTransactionId: transaction.GatewayTransactionId,
            Status: transaction.Status.ToString(),
            PaidAt: transaction.PaidAt,
            IsAuthoritative: true);
}
