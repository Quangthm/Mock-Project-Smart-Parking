using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Persistence;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Ports;
using SmartParking.NotificationAuditService.Application.DTOs.Notifications;
using SmartParking.NotificationAuditService.Domain.Entities;

namespace SmartParking.NotificationAuditService.Infrastructure.Adapters.Delivery;

public class NotificationDispatcher : INotificationDispatcher
{
    private readonly IEnumerable<INotificationChannelAdapter> _adapters;
    private readonly INotificationTemplateRepository _templateRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        IEnumerable<INotificationChannelAdapter> adapters,
        INotificationTemplateRepository templateRepository,
        INotificationRepository notificationRepository,
        IUnitOfWork unitOfWork,
        ILogger<NotificationDispatcher> logger)
    {
        _adapters = adapters;
        _templateRepository = templateRepository;
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<NotificationDeliveryResult> DispatchAsync(
        DurableNotification notification,
        CancellationToken cancellationToken = default)
    {
        // 1. Mark processing (increments attempts)
        notification.MarkProcessing();
        await _notificationRepository.UpdateAsync(notification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // 2. Select appropriate adapter for the channel
        var adapter = _adapters.FirstOrDefault(a => a.Channel == notification.Channel);
        if (adapter == null)
        {
            var error = $"No registered delivery adapter found for channel {notification.Channel}";
            _logger.LogError(
                "Dispatch failed for notification {NotificationId}: {ErrorMessage}",
                notification.Id,
                error);

            notification.RecordFailure(error);
            await _notificationRepository.UpdateAsync(notification, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return NotificationDeliveryResult.Failed(notification.Id, notification.Attempts, error);
        }

        // 3. Lookup template and render content
        string? subject = null;
        string body = string.Empty;

        var template = await _templateRepository.GetActiveByCodeAsync(notification.TemplateCode, cancellationToken);
        if (template != null)
        {
            subject = template.Subject;
            body = RenderTemplate(template.BodyContent, notification.Payload);
        }
        else
        {
            _logger.LogWarning(
                "Active template {TemplateCode} not found for notification {NotificationId}. Using fallback body.",
                notification.TemplateCode,
                notification.Id);

            subject = $"Notice: {notification.OriginatingEvent}";
            body = $"Notification for {notification.OriginatingEvent}. Payload: {notification.Payload}";
        }

        // 4. Perform delivery via adapter
        try
        {
            var sendResult = await adapter.SendAsync(
                notification.Id,
                notification.RecipientId,
                subject,
                body,
                notification.Payload,
                cancellationToken);

            if (sendResult.Success)
            {
                notification.MarkDelivered();
                await _notificationRepository.UpdateAsync(notification, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Notification {NotificationId} successfully delivered via {Provider} on attempt {Attempts}.",
                    notification.Id,
                    sendResult.DeliveryProvider,
                    notification.Attempts);

                return NotificationDeliveryResult.Delivered(notification.Id, notification.Attempts);
            }
            else
            {
                var error = sendResult.ErrorMessage ?? "Channel delivery failed";
                _logger.LogWarning(
                    "Delivery attempt {Attempts} failed for notification {NotificationId}: {ErrorMessage}",
                    notification.Attempts,
                    notification.Id,
                    error);

                notification.RecordFailure(error);
                await _notificationRepository.UpdateAsync(notification, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return NotificationDeliveryResult.Failed(notification.Id, notification.Attempts, error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Exception thrown during adapter delivery for notification {NotificationId}.",
                notification.Id);

            notification.RecordFailure(ex.Message);
            await _notificationRepository.UpdateAsync(notification, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return NotificationDeliveryResult.Failed(notification.Id, notification.Attempts, ex.Message);
        }
    }

    private static string RenderTemplate(string templateContent, string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson) || payloadJson == "{}")
        {
            return templateContent;
        }

        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var rendered = templateContent;
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                var placeholder = $"{{{{{property.Name}}}}}";
                rendered = rendered.Replace(placeholder, property.Value.ToString(), StringComparison.OrdinalIgnoreCase);
            }
            return rendered;
        }
        catch
        {
            return templateContent;
        }
    }
}
