using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.IoTService.API.Http;
using SmartParking.IoTService.Application.Assistant;

namespace SmartParking.IoTService.API.Controllers;

[ApiController]
[Route("api/assistant")]
[Authorize(Policy = "AssistantDriver")]
public sealed class AssistantController(IMediator mediator) : ControllerBase
{
    [HttpGet("capabilities")]
    public async Task<IActionResult> Capabilities(CancellationToken cancellationToken)
    {
        if (Request.QueryString.HasValue)
            return BadRequest(AssistantErrors.Envelope(HttpContext, AssistantErrors.ForStatus(400)));
        var result = await mediator.Send(new GetAssistantCapabilitiesQuery(), cancellationToken);
        return Ok(new SuccessEnvelope<AssistantCapabilities>(true, result, HttpContext.TraceIdentifier));
    }
}
