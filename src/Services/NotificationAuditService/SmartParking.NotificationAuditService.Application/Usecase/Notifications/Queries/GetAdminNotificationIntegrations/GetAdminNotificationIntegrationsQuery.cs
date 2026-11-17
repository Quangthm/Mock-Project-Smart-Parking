using MediatR;
using SmartParking.NotificationAuditService.Application.Common.Interfaces.Ports;
using SmartParking.NotificationAuditService.Domain.Enums;

namespace SmartParking.NotificationAuditService.Application.Usecase.Notifications.Queries.GetAdminNotificationIntegrations;

public sealed record GetAdminNotificationIntegrationsQuery : IRequest<AdminNotificationIntegrationsResult>;

public sealed record AdminNotificationIntegrationsResult(IReadOnlyList<AdminNotificationChannelDto> Channels);

public sealed record AdminNotificationChannelDto(
    string Channel,
    string Provider,
    bool IsReady,
    string Status,
    string Description,
    DateTimeOffset? LastTestedAt);

public sealed class GetAdminNotificationIntegrationsHandler(IEnumerable<INotificationChannelAdapter> channelAdapters)
    : IRequestHandler<GetAdminNotificationIntegrationsQuery, AdminNotificationIntegrationsResult>
{
    private static readonly NotificationChannel[] ChannelOrder =
    {
        NotificationChannel.EMAIL,
        NotificationChannel.SMS,
        NotificationChannel.PUSH,
        NotificationChannel.IN_APP
    };

    private static readonly IReadOnlyDictionary<NotificationChannel, (string Provider, string Description)> Fixtures =
        new Dictionary<NotificationChannel, (string, string)>
        {
            [NotificationChannel.EMAIL] = (
                "LOCAL_EMAIL_CAPTURE_FIXTURE",
                "Bắt và ghi nhận email hóa đơn / biên lai vào bộ nhớ cục bộ phục vụ kiểm thử và đối soát."),
            [NotificationChannel.SMS] = (
                "LOCAL_SMS_FIXTURE",
                "Giả lập gửi SMS mã OTP vào bộ nhớ cục bộ, không phát sinh cuộc gọi ra mạng ngoài. Kênh sẵn sàng hoạt động."),
            [NotificationChannel.PUSH] = (
                "LOCAL_PUSH_FIXTURE",
                "Giả lập gửi thông báo WebPush / App vào bộ nhớ cục bộ, không phát sinh cuộc gọi ra mạng ngoài. Kênh sẵn sàng hoạt động."),
            [NotificationChannel.IN_APP] = (
                "IN_APP_FIXTURE",
                "Lưu thông báo trực tiếp vào bảng notifications để hiển thị trên web portal.")
        };

    public Task<AdminNotificationIntegrationsResult> Handle(
        GetAdminNotificationIntegrationsQuery request,
        CancellationToken cancellationToken)
    {
        var registeredChannels = channelAdapters
            .Select(adapter => adapter.Channel)
            .ToHashSet();

        var channels = ChannelOrder
            .Where(registeredChannels.Contains)
            .Select(channel =>
            {
                var fixture = Fixtures[channel];
                return new AdminNotificationChannelDto(
                    Channel: channel.ToString(),
                    Provider: fixture.Provider,
                    IsReady: true,
                    Status: "ACTIVE",
                    Description: fixture.Description,
                    LastTestedAt: null);
            })
            .ToArray();

        return Task.FromResult(new AdminNotificationIntegrationsResult(channels));
    }
}
