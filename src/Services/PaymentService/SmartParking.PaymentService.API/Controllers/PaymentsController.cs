using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.PaymentService.Application.DTOs.Payments;
using SmartParking.PaymentService.Application.Usecase.ConfirmCashPayment;
using SmartParking.PaymentService.Application.Usecase.CreatePaymentOrder;
using SmartParking.PaymentService.Application.Usecase.GetPaymentStatus;
using SmartParking.PaymentService.Application.Usecase.InitiateCheckout;
using SmartParking.PaymentService.Application.Usecase.ProcessWebhook;

namespace SmartParking.PaymentService.API.Controllers;

[ApiController]
[Route("api/v1/payments")]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<PaymentsController> _logger;
    private readonly IHostEnvironment _environment;

    public PaymentsController(
        IMediator _mediator,
        ILogger<PaymentsController> logger,
        IHostEnvironment environment)
    {
        this._mediator = _mediator;
        _logger = logger;
        _environment = environment;
    }

    [HttpPost("orders")]
    [Authorize]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreatePaymentOrderRequestDto request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var effectiveKey = string.IsNullOrWhiteSpace(idempotencyKey)
            ? request.IdempotencyKey
            : idempotencyKey;
        var result = await _mediator.Send(
            new CreatePaymentOrderCommand(request with { IdempotencyKey = effectiveKey }),
            cancellationToken);
        return Ok(new { success = true, data = result });
    }

    [HttpPost("orders/{orderId:guid}/cash")]
    [Authorize]
    public async Task<IActionResult> ConfirmCash(
        Guid orderId,
        [FromBody] CashPaymentRequestDto? request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ConfirmCashPaymentCommand(
                orderId,
                Domain.Enums.PaymentMethod.CASH,
                request?.IdempotencyKey),
            cancellationToken);
        return Ok(new { success = true, data = result });
    }

    [HttpPost("orders/{orderId:guid}/demo-pay")]
    [Authorize]
    public async Task<IActionResult> DemoPay(Guid orderId, CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment())
            return NotFound();

        var result = await _mediator.Send(
            new ConfirmCashPaymentCommand(orderId, Domain.Enums.PaymentMethod.PAYOS),
            cancellationToken);
        return Ok(new { success = true, data = result, demo = true });
    }

    /// <summary>
    /// Khởi tạo liên kết thanh toán payOS VietQR cho đơn hàng
    /// </summary>
    [HttpPost("orders/{orderId:guid}/checkout")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> InitiateCheckout(
        [FromRoute] Guid orderId,
        [FromBody] PaymentCheckoutRequestDto? request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var command = new InitiatePaymentCheckoutCommand(
            OrderId: orderId,
            IdempotencyKey: idempotencyKey,
            PaymentMethod: request?.PaymentMethod ?? Domain.Enums.PaymentMethod.PAYOS,
            ReturnUrl: request?.ReturnUrl,
            CancelUrl: request?.CancelUrl
        );

        var result = await _mediator.Send(command, cancellationToken);

        if (result.IsReplayed)
        {
            Response.Headers["Idempotency-Replayed"] = "true";
        }

        return Ok(new
        {
            success = true,
            data = result
        });
    }

    /// <summary>
    /// Tiếp nhận Webhook IPN từ cổng thanh toán payOS
    /// </summary>
    [HttpPost("callbacks/payos")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ProcessPayOsWebhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        string signature = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            if (doc.RootElement.TryGetProperty("signature", out var sigProp))
            {
                signature = sigProp.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            return BadRequest(new
            {
                success = false,
                errorCode = "INVALID_JSON_PAYLOAD",
                message = "Payload webhook không phải định dạng JSON hợp lệ."
            });
        }

        var command = new ProcessPayOsWebhookCommand(rawBody, signature);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new
            {
                success = false,
                errorCode = result.ErrorCode ?? "WEBHOOK_PROCESSING_FAILED",
                message = result.Message
            });
        }

        return Ok(new
        {
            success = true,
            message = result.Message
        });
    }

    /// <summary>
    /// Tra cứu trạng thái giao dịch thanh toán (hỗ trợ chủ động đối soát với payOS)
    /// </summary>
    [HttpGet("transactions/{transactionId:guid}/status")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTransactionStatus(
        [FromRoute] Guid transactionId,
        CancellationToken cancellationToken)
    {
        var query = new GetPaymentStatusQuery(transactionId);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(new
        {
            success = true,
            data = result
        });
    }
}
