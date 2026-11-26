using SmartParking.PaymentService.Application.Reservations;
using System.Diagnostics;
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

    [HttpGet("reservations/{reservationId:guid}/checkout-state")]
    [Authorize(Roles = "DRIVER,driver")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ReservationCheckout(Guid reservationId, CancellationToken ct) =>
        Ok(new { success = true, data = await _mediator.Send(new ReadReservationCheckout(AuthenticatedAccountId(), reservationId, null), ct) });

    [HttpGet("orders/{orderId:guid}/reservation")]
    [Authorize(Roles = "DRIVER,driver")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> OrderReservation(Guid orderId, CancellationToken ct) =>
        Ok(new { success = true, data = await _mediator.Send(new ReadReservationCheckout(AuthenticatedAccountId(), null, orderId), ct) });

    private Guid AuthenticatedAccountId() =>
        Guid.TryParse(User.FindFirst("account_id")?.Value, out var account) ? account : Guid.Empty;

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
        if (request.DevelopmentTestAmount.HasValue && !_environment.IsDevelopment())
            return NotFound();
        if (request.DevelopmentTestAmount.HasValue && request.DevelopmentTestAmount.Value != 2000m)
            return BadRequest(new { success = false, code = "INVALID_TEST_AMOUNT", message = "The development PayOS test amount is fixed at 2,000 VND." });

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
            DepositAmount: request.DepositAmount,
            DevelopmentTestAmount: request.DevelopmentTestAmount
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
    [HttpPost("payos/webhook")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ProcessPayOsWebhook(CancellationToken cancellationToken)
    {
        var started = Stopwatch.GetTimestamp();
        var traceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        HttpContext.Items[typeof(ProcessPayOsWebhookCommand)] = started;
        using var scope = _logger.BeginScope(new Dictionary<string, object?> { ["TraceId"] = traceId });
        _logger.LogInformation("PayOS webhook arrived. TraceId {TraceId}", traceId);

        IActionResult Complete(int status, string? code, object body)
        {
            _logger.LogInformation("PayOS webhook completed. TraceId {TraceId}, HTTP {StatusCode}, code {ErrorCode}, elapsed {ElapsedMs} ms",
                traceId, status, code, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            return StatusCode(status, body);
        }

        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        string signature = string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("signature", out var sigProp) && sigProp.ValueKind == JsonValueKind.String)
            {
                signature = sigProp.GetString() ?? string.Empty;
            }
        }
        catch (JsonException)
        {
            return Complete(400, "INVALID_JSON_PAYLOAD", new
            {
                success = false,
                errorCode = "INVALID_JSON_PAYLOAD",
                message = "The webhook payload is not valid JSON."
            });
        }

        var command = new ProcessPayOsWebhookCommand(rawBody, signature);
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            var failure = new
            {
                success = false,
                errorCode = result.ErrorCode ?? "WEBHOOK_PROCESSING_FAILED",
                message = result.Message
            };
            return Complete(result.Retryable ? 503 : 400, failure.errorCode, failure);
        }

        return Complete(200, null, new
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
