using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.PaymentService.Application.DTOs.Invoices;
using SmartParking.PaymentService.Application.DTOs.Payments;
using SmartParking.PaymentService.Application.Usecase.CollectCashPayment;
using SmartParking.PaymentService.Application.Usecase.ConfirmCashPayment;
using SmartParking.PaymentService.Application.Usecase.CreatePaymentOrder;
using SmartParking.PaymentService.Application.Usecase.GetInvoice;
using SmartParking.PaymentService.Application.Usecase.GetPaymentStatus;
using SmartParking.PaymentService.Application.Usecase.InitiateCheckout;
using SmartParking.PaymentService.Application.Usecase.PrepareParkingFeeOrder;
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
        IMediator mediator,
        ILogger<PaymentsController> logger,
        IHostEnvironment environment)
    {
        _mediator = mediator;
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
    /// S2S Checkout Preparation API: Chuẩn bị đơn hàng thanh toán cước đỗ xe
    /// </summary>
    [HttpPost("orders/prepare")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> PrepareOrder(
        [FromBody] PrepareParkingFeeOrderRequestDto request,
        CancellationToken cancellationToken)
    {
        var command = new PrepareParkingFeeOrderCommand(
            TenantId: request.TenantId,
            SiteId: request.SiteId,
            SessionId: request.SessionId,
            VehicleType: request.VehicleType,
            PlateNumber: request.PlateNumber,
            EntryTime: request.EntryTime,
            PricingCutoffAt: request.PricingCutoffAt,
            AccountId: request.AccountId,
            PenaltyAmount: request.PenaltyAmount,
            PenaltyReason: request.PenaltyReason,
            IdempotencyKey: request.IdempotencyKey,
            DepositAmount: request.DepositAmount
        );

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(new
        {
            success = true,
            data = result
        });
    }

    /// <summary>
    /// Tra cứu Hóa đơn tài chính chính thức theo Order ID
    /// </summary>
    [HttpGet("orders/{orderId:guid}/invoice")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetInvoiceByOrder(
        [FromRoute] Guid orderId,
        CancellationToken cancellationToken)
    {
        var query = new GetInvoiceByOrderIdQuery(orderId);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(new
        {
            success = true,
            data = result
        });
    }

    /// <summary>
    /// Ghi nhận thanh toán tiền mặt tại bốt trực cổng và xuất hóa đơn chính thức
    /// </summary>
    [HttpPost("orders/{orderId:guid}/cash")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CollectCashPayment(
        [FromRoute] Guid orderId,
        [FromBody] CashPaymentRequestDto? request,
        CancellationToken cancellationToken)
    {
        var command = new CollectCashPaymentCommand(
            OrderId: orderId,
            AmountReceived: request?.AmountReceived,
            Notes: request?.Notes,
            IdempotencyKey: request?.IdempotencyKey
        );

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(new
        {
            success = true,
            data = result
        });
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
