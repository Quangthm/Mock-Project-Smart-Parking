using System.Reflection;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartParking.NotificationAuditService.API.Controllers;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Services;
using SmartParking.NotificationAuditService.Application.DTOs.Notifications;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.MarkAllNotificationsRead;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.MarkNotificationRead;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Commands.RetryNotificationDelivery;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Queries.GetDriverNotifications;
using SmartParking.NotificationAuditService.Application.Usecase.Notifications.Queries.GetNotificationById;
using SmartParking.NotificationAuditService.Domain.Enums;
using SmartParking.NotificationAuditService.Domain.Exceptions;

namespace SmartParking.NotificationAuditService.Tests.Controllers;

public class NotificationsControllerTests
{
    private class FakeCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; set; }
        public string? UserRole { get; set; }
        public bool IsAuthenticated { get; set; }
    }

    private class FakeMediator : IMediator
    {
        public object? NextResponse { get; set; }
        public Exception? ExceptionToThrow { get; set; }
        public object? LastSentRequest { get; private set; }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            LastSentRequest = request;
            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult((TResponse)NextResponse!);
        }

        public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        {
            LastSentRequest = request;
            if (ExceptionToThrow != null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(NextResponse);
        }

        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification => Task.CompletedTask;

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        {
            LastSentRequest = request;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public void Controller_ShouldHaveAuthorizeAttributeAndDualRoutes()
    {
        var type = typeof(NotificationsController);

        // Check [Authorize]
        var authAttr = type.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authAttr);

        // Check dual routes
        var routeAttrs = type.GetCustomAttributes<RouteAttribute>().ToList();
        Assert.Equal(2, routeAttrs.Count);
        Assert.Contains(routeAttrs, r => r.Template == "api/v1/notifications");
        Assert.Contains(routeAttrs, r => r.Template == "api/notifications");
    }

    [Fact]
    public async Task MarkAsRead_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var mediator = new FakeMediator();
        var currentUserService = new FakeCurrentUserService { IsAuthenticated = false, UserId = null };
        var controller = new NotificationsController(mediator, currentUserService);

        // Act
        var result = await controller.MarkAsRead(Guid.NewGuid());

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task MarkAsRead_WhenNotificationNotFound_ReturnsNotFound()
    {
        // Arrange
        var mediator = new FakeMediator { NextResponse = null };
        var currentUserId = Guid.NewGuid();
        var currentUserService = new FakeCurrentUserService { IsAuthenticated = true, UserId = currentUserId };
        var controller = new NotificationsController(mediator, currentUserService);
        var targetNotificationId = Guid.NewGuid();

        // Act
        var result = await controller.MarkAsRead(targetNotificationId);

        // Assert
        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(404, notFoundResult.StatusCode);

        var sentCommand = Assert.IsType<MarkNotificationReadCommand>(mediator.LastSentRequest);
        Assert.Equal(targetNotificationId, sentCommand.Id);
        Assert.Equal(currentUserId, sentCommand.RequestingUserId);
    }

    [Fact]
    public async Task MarkAsRead_WhenSuccessful_ReturnsOkWithNotificationDto()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var expectedDto = new NotificationDto
        {
            Id = notificationId,
            RecipientId = currentUserId,
            Title = "Payment Confirmed",
            Message = "Your parking session has been paid.",
            Status = NotificationStatus.DELIVERED.ToString(),
            Channel = NotificationChannel.IN_APP.ToString(),
            IsRead = true,
            ReadAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        };

        var mediator = new FakeMediator { NextResponse = expectedDto };
        var currentUserService = new FakeCurrentUserService { IsAuthenticated = true, UserId = currentUserId };
        var controller = new NotificationsController(mediator, currentUserService);

        // Act
        var result = await controller.MarkAsRead(notificationId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        var sentCommand = Assert.IsType<MarkNotificationReadCommand>(mediator.LastSentRequest);
        Assert.Equal(notificationId, sentCommand.Id);
        Assert.Equal(currentUserId, sentCommand.RequestingUserId);
    }

    [Fact]
    public async Task MarkAsRead_WhenUserMismatch_PropagatesForbiddenException()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var mediator = new FakeMediator
        {
            ExceptionToThrow = new NotificationForbiddenException("You do not have permission to modify this notification.")
        };
        var currentUserService = new FakeCurrentUserService { IsAuthenticated = true, UserId = currentUserId };
        var controller = new NotificationsController(mediator, currentUserService);

        // Act & Assert
        await Assert.ThrowsAsync<NotificationForbiddenException>(() => controller.MarkAsRead(notificationId));
    }

    [Fact]
    public async Task MarkAllAsRead_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var mediator = new FakeMediator();
        var currentUserService = new FakeCurrentUserService { IsAuthenticated = false, UserId = null };
        var controller = new NotificationsController(mediator, currentUserService);

        // Act
        var result = await controller.MarkAllAsRead();

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task MarkAllAsRead_WhenSuccessful_ReturnsOkWithMarkedCount()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        const int expectedMarkedCount = 7;
        var mediator = new FakeMediator { NextResponse = expectedMarkedCount };
        var currentUserService = new FakeCurrentUserService { IsAuthenticated = true, UserId = currentUserId };
        var controller = new NotificationsController(mediator, currentUserService);

        // Act
        var result = await controller.MarkAllAsRead();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        var sentCommand = Assert.IsType<MarkAllNotificationsReadCommand>(mediator.LastSentRequest);
        Assert.Equal(currentUserId, sentCommand.RecipientId);
    }

    [Fact]
    public async Task GetNotifications_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var mediator = new FakeMediator();
        var currentUserService = new FakeCurrentUserService { IsAuthenticated = false, UserId = null };
        var controller = new NotificationsController(mediator, currentUserService);

        // Act
        var result = await controller.GetNotifications(null);

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task GetNotifications_WhenAuthenticated_ReturnsOkWithNotifications()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var list = new List<NotificationDto>
        {
            new() { Id = Guid.NewGuid(), RecipientId = currentUserId, Title = "Test" }
        };
        var mediator = new FakeMediator { NextResponse = list };
        var currentUserService = new FakeCurrentUserService { IsAuthenticated = true, UserId = currentUserId };
        var controller = new NotificationsController(mediator, currentUserService);

        // Act
        var result = await controller.GetNotifications(NotificationStatus.DELIVERED, 20);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        var sentQuery = Assert.IsType<GetDriverNotificationsQuery>(mediator.LastSentRequest);
        Assert.Equal(currentUserId, sentQuery.RecipientId);
        Assert.Equal(NotificationStatus.DELIVERED, sentQuery.Status);
        Assert.Equal(20, sentQuery.Limit);
    }

    [Fact]
    public async Task GetNotificationById_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var mediator = new FakeMediator();
        var currentUserService = new FakeCurrentUserService { IsAuthenticated = false, UserId = null };
        var controller = new NotificationsController(mediator, currentUserService);

        // Act
        var result = await controller.GetNotificationById(Guid.NewGuid());

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task GetNotificationById_WhenAuthenticated_ReturnsOk()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var notifId = Guid.NewGuid();
        var dto = new NotificationDto { Id = notifId, RecipientId = currentUserId, Title = "Detail" };

        var mediator = new FakeMediator { NextResponse = dto };
        var currentUserService = new FakeCurrentUserService { IsAuthenticated = true, UserId = currentUserId, UserRole = "driver" };
        var controller = new NotificationsController(mediator, currentUserService);

        // Act
        var result = await controller.GetNotificationById(notifId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        var sentQuery = Assert.IsType<GetNotificationByIdQuery>(mediator.LastSentRequest);
        Assert.Equal(notifId, sentQuery.NotificationId);
        Assert.Equal(currentUserId, sentQuery.RequestingUserId);
        Assert.Equal("driver", sentQuery.RequestingUserRole);
    }

    [Fact]
    public async Task RetryDelivery_WhenUnauthenticated_ReturnsUnauthorized()
    {
        // Arrange
        var mediator = new FakeMediator();
        var currentUserService = new FakeCurrentUserService { IsAuthenticated = false, UserId = null };
        var controller = new NotificationsController(mediator, currentUserService);

        // Act
        var result = await controller.RetryDelivery(Guid.NewGuid());

        // Assert
        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
    }

    [Fact]
    public async Task RetryDelivery_WhenAuthenticated_ReturnsOk()
    {
        // Arrange
        var currentUserId = Guid.NewGuid();
        var notifId = Guid.NewGuid();
        var deliveryResult = NotificationDeliveryResult.Delivered(notifId, 1);

        var mediator = new FakeMediator { NextResponse = deliveryResult };
        var currentUserService = new FakeCurrentUserService { IsAuthenticated = true, UserId = currentUserId, UserRole = "driver" };
        var controller = new NotificationsController(mediator, currentUserService);

        // Act
        var result = await controller.RetryDelivery(notifId);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(200, okResult.StatusCode);

        var sentCommand = Assert.IsType<RetryNotificationDeliveryCommand>(mediator.LastSentRequest);
        Assert.Equal(notifId, sentCommand.NotificationId);
        Assert.Equal(currentUserId, sentCommand.RequestingUserId);
    }
}
