using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.PaymentService.Application.DTOs.Invoices;
using SmartParking.PaymentService.Application.Usecase.GetInvoice;
using SmartParking.PaymentService.Application.Usecase.GetInvoicePdf;
using SmartParking.PaymentService.Application.Usecase.GetMyInvoices;

namespace SmartParking.PaymentService.API.Controllers;

[ApiController]
[Route("api/v1/payments/invoices")]
public class InvoicesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<InvoicesController> _logger;

    public InvoicesController(IMediator mediator, ILogger<InvoicesController> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    /// <summary>
    /// Authoritative Customer Invoice by ID: Tra cứu Hóa đơn theo Invoice ID
    /// </summary>
    [HttpGet("{invoiceId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetInvoiceById(
        [FromRoute] Guid invoiceId,
        CancellationToken cancellationToken)
    {
        var query = new GetInvoiceByIdQuery(invoiceId);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(new
        {
            success = true,
            data = result
        });
    }

    /// <summary>
    /// Authoritative Customer Invoice by Session: Tra cứu Hóa đơn đối soát theo Session ID
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(object), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(object), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetInvoiceBySession(
        [FromQuery] Guid? sessionId,
        CancellationToken cancellationToken)
    {
        if (!sessionId.HasValue || sessionId.Value == Guid.Empty)
        {
            return BadRequest(new
            {
                success = false,
                errorCode = "MISSING_SESSION_ID",
                message = "The sessionId query parameter is required."
            });
        }

        var query = new GetInvoiceBySessionIdQuery(sessionId.Value);
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(new
        {
            success = true,
            data = result
        });
    }

    /// <summary>
    /// Authoritative Customer Invoice PDF Download: Tải file hóa đơn điện tử PDF chính thức
    /// </summary>
    [HttpGet("{invoiceId:guid}/download")]
    [Authorize(Roles = "DRIVER,CUSTOMER,OPERATOR,ADMIN")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> DownloadInvoicePdf(
        [FromRoute] Guid invoiceId,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DownloadInvoicePdfQuery(invoiceId), cancellationToken);
        return File(result.Content, result.ContentType, result.FileName);
    }

    /// <summary>
    /// Driver Invoice History: Tra cứu lịch sử hóa đơn của tài xế hiện tại kèm lọc và phân trang
    /// </summary>
    [HttpGet("my-history")]
    [Authorize(Roles = "DRIVER,CUSTOMER")]
    [ProducesResponseType(typeof(PagedResult<CustomerInvoiceResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMyHistory(
        [FromQuery] string? status,
        [FromQuery] DateTimeOffset? fromDate,
        [FromQuery] DateTimeOffset? toDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = new GetMyPaymentInvoicesQuery(status, fromDate, toDate, pageNumber, pageSize);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }
}
