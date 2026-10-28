using MediatR;
using Microsoft.AspNetCore.Mvc;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Services;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.RetryNotificationDelivery;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Queries.GetDriverNotifications;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Queries.GetNotificationById;
using SmartParking.NotificationAuditService.Domain.Enums;

namespace SmartParking.NotificationAuditService.API.Controllers;

[ApiController]
[Route("api/notifications")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUserService;

    public NotificationsController(IMediator mediator, ICurrentUserService currentUserService)
    {
        _mediator = mediator;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] NotificationStatus? status,
        [FromQuery] int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Authentication is required to access notifications.");
        }

        var query = new GetDriverNotificationsQuery
        {
            RecipientId = _currentUserService.UserId.Value,
            Status = status,
            Limit = limit
        };

        var notifications = await _mediator.Send(query, cancellationToken);
        return Ok(new
        {
            success = true,
            data = notifications
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetNotificationById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Authentication is required to view notification details.");
        }

        var query = new GetNotificationByIdQuery
        {
            NotificationId = id,
            RequestingUserId = _currentUserService.UserId.Value,
            RequestingUserRole = _currentUserService.UserRole ?? "Driver"
        };

        var notification = await _mediator.Send(query, cancellationToken);
        return Ok(new
        {
            success = true,
            data = notification
        });
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> RetryDelivery(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("Authentication is required to retry notification delivery.");
        }

        var command = new RetryNotificationDeliveryCommand
        {
            NotificationId = id,
            RequestingUserId = _currentUserService.UserId.Value,
            RequestingUserRole = _currentUserService.UserRole ?? "Driver"
        };

        var result = await _mediator.Send(command, cancellationToken);
        return Ok(new
        {
            success = true,
            data = result
        });
    }
}
