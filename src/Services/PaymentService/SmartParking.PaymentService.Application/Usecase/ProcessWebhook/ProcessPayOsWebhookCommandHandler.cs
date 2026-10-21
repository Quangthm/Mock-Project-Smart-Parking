using MediatR;
using Microsoft.Extensions.Logging;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Ports;

namespace SmartParking.PaymentService.Application.Usecase.ProcessWebhook;

public class ProcessPayOsWebhookCommandHandler : IRequestHandler<ProcessPayOsWebhookCommand, ProcessPayOsWebhookResult>
{
    private readonly IPaymentGatewayPort _gatewayPort;
    private readonly IProviderWebhookRepository _webhookRepository;
    private readonly IPaymentOrderRepository _orderRepository;
    private readonly IPaymentTransactionRepository _transactionRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProcessPayOsWebhookCommandHandler> _logger;

    public ProcessPayOsWebhookCommandHandler(
        IPaymentGatewayPort gatewayPort,
        IProviderWebhookRepository webhookRepository,
        IPaymentOrderRepository orderRepository,
        IPaymentTransactionRepository transactionRepository,
        IUnitOfWork unitOfWork,
        ILogger<ProcessPayOsWebhookCommandHandler> logger)
    {
        _gatewayPort = gatewayPort;
        _webhookRepository = webhookRepository;
        _orderRepository = orderRepository;
        _transactionRepository = transactionRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ProcessPayOsWebhookResult> Handle(ProcessPayOsWebhookCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify and parse webhook payload
        var parseResult = await _gatewayPort.VerifyAndParseWebhookAsync(request.RawPayload, request.Signature, cancellationToken);
        if (!parseResult.IsValidSignature)
        {
            _logger.LogWarning("Webhook signature verification failed for payOS callback.");
            return new ProcessPayOsWebhookResult(false, "Chữ ký webhook không hợp lệ.", "INVALID_WEBHOOK_SIGNATURE");
        }

        var eventId = parseResult.BankReference ?? parseResult.GatewayTransactionId ?? parseResult.OrderCode.ToString();

        // 2. Check duplicate webhook idempotency
        var existingWebhook = await _webhookRepository.GetByProviderAndEventIdAsync("PAYOS", eventId, cancellationToken);
        if (existingWebhook != null && existingWebhook.ProcessingState == WebhookProcessingState.PROCESSED)
        {
            _logger.LogInformation("PayOS webhook event {EventId} was already processed. Replaying success response.", eventId);
            return new ProcessPayOsWebhookResult(true, "Webhook đã được xử lý thành công trước đó (Idempotent).");
        }

        if (existingWebhook == null)
        {
            existingWebhook = new ProviderWebhook
            {
                Provider = "PAYOS",
                ProviderEventId = eventId,
                ProcessingState = WebhookProcessingState.PENDING,
                Payload = request.RawPayload,
                ReceivedAt = DateTimeOffset.UtcNow
            };
            await _webhookRepository.AddAsync(existingWebhook, cancellationToken);
        }

        // 3. Find payment order
        var order = await _orderRepository.GetByOrderCodeAsync(parseResult.OrderCode.ToString(), cancellationToken);
        if (order == null)
        {
            _logger.LogWarning("PaymentOrder with OrderCode {OrderCode} not found for webhook event {EventId}.", parseResult.OrderCode, eventId);
            existingWebhook.MarkFailed();
            _webhookRepository.Update(existingWebhook);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ProcessPayOsWebhookResult(false, $"Không tìm thấy đơn hàng tương ứng với orderCode {parseResult.OrderCode}.", "ORDER_NOT_FOUND");
        }

        // 4. Validate amount
        if ((long)order.FinalAmount != parseResult.AmountVnd)
        {
            _logger.LogError("Payment amount mismatch for OrderCode {OrderCode}: expected {Expected}, received {Received}.",
                order.OrderCode, order.FinalAmount, parseResult.AmountVnd);

            existingWebhook.MarkFailed();
            _webhookRepository.Update(existingWebhook);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new ProcessPayOsWebhookResult(false, "Số tiền thanh toán không khớp với đơn hàng (AMOUNT_MISMATCH).", "AMOUNT_MISMATCH");
        }

        // 5. Update state on SUCCESS
        if (parseResult.PaymentStatus == "SUCCESS")
        {
            var tx = order.Transactions.OrderByDescending(t => t.CreatedAt).FirstOrDefault();
            if (tx == null)
            {
                tx = new PaymentTransaction
                {
                    Id = Guid.NewGuid(),
                    TenantId = order.TenantId,
                    PaymentOrderId = order.Id,
                    TransactionType = TransactionType.PAYMENT,
                    PaymentMethod = PaymentMethod.PAYOS,
                    GatewayTransactionId = parseResult.GatewayTransactionId,
                    Amount = order.FinalAmount,
                    Status = PaymentStatus.PENDING,
                    CreatedAt = DateTimeOffset.UtcNow
                };
                await _transactionRepository.AddAsync(tx, cancellationToken);
            }

            tx.MarkSuccess(parseResult.GatewayTransactionId, parseResult.TransactionTime);
            order.MarkPaid(parseResult.TransactionTime);
            existingWebhook.MarkProcessed();

            _transactionRepository.Update(tx);
            _orderRepository.Update(order);
            _webhookRepository.Update(existingWebhook);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Successfully processed payOS payment webhook for OrderCode {OrderCode}, TxId {TxId}.", order.OrderCode, tx.Id);
        }

        return new ProcessPayOsWebhookResult(true, "Webhook processed successfully.");
    }
}
