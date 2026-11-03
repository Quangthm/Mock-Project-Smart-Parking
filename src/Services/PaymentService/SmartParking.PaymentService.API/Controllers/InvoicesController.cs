using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.PaymentService.Application.Usecase.GetInvoice;

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
                message = "Tham số truy vấn sessionId là bắt buộc."
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
}
