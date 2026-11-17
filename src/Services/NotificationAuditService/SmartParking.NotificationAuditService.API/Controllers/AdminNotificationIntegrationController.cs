using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Queries.GetAdminNotificationIntegrations;

namespace SmartParking.NotificationAuditService.API.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("api/v1/admin/integrations/notifications")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AdminNotificationIntegrationController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetNotificationIntegrations(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAdminNotificationIntegrationsQuery(), cancellationToken);
        return Ok(new
        {
            success = true,
            data = result
        });
    }
}
