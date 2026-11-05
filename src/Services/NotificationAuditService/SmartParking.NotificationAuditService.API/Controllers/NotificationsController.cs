using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Services;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.MarkAllNotificationsRead;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.MarkNotificationRead;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.RetryNotificationDelivery;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Queries.GetDriverNotifications;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Queries.GetNotificationById;
using SmartParking.NotificationAuditService.Domain.Enums;

namespace SmartParking.NotificationAuditService.API.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/notifications")]
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
            return Unauthorized(new { success = false, message = "Authentication is required to access notifications." });
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
            return Unauthorized(new { success = false, message = "Authentication is required to view notification details." });
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

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            return Unauthorized(new { success = false, message = "Authentication is required to mark notification as read." });
        }

        var command = new MarkNotificationReadCommand(id, _currentUserService.UserId.Value);
        var result = await _mediator.Send(command, cancellationToken);
        if (result == null)
        {
            return NotFound(new { success = false, message = $"Notification with ID '{id}' was not found." });
        }

        return Ok(new
        {
            success = true,
            data = result
        });
    }

    [HttpPost("mark-all-read")]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            return Unauthorized(new { success = false, message = "Authentication is required to mark all notifications as read." });
        }

        var command = new MarkAllNotificationsReadCommand(_currentUserService.UserId.Value);
        var count = await _mediator.Send(command, cancellationToken);

        return Ok(new
        {
            success = true,
            data = new
            {
                markedCount = count,
                updatedCount = count
            },
            markedCount = count
        });
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> RetryDelivery(
        [FromRoute] Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!_currentUserService.IsAuthenticated || !_currentUserService.UserId.HasValue)
        {
            return Unauthorized(new { success = false, message = "Authentication is required to retry notification delivery." });
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
