using Microsoft.EntityFrameworkCore;
using SmartParking.NotificationAuditService.Domain.Entities;
using SmartParking.NotificationAuditService.Domain.Enums;
using SmartParking.NotificationAuditService.Persistence.Context;

namespace SmartParking.NotificationAuditService.Persistence.Seed;

public static class NotificationTemplateSeeder
{
    public static async Task SeedAsync(NotificationAuditDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.NotificationTemplates.AnyAsync(cancellationToken))
        {
            return;
        }

        var templates = new List<NotificationTemplate>
        {
            NotificationTemplate.Create(
                templateCode: "RESERVATION_CONFIRMED",
                channel: NotificationChannel.IN_APP,
                subject: "Đặt chỗ đỗ xe thành công",
                bodyContent: "Yêu cầu đặt chỗ tại {{siteName}} (Vị trí {{slotCode}}) đã được xác nhận từ {{startTime}} đến {{endTime}}. Mã đặt chỗ: {{reservationId}}.",
                version: 1),

            NotificationTemplate.Create(
                templateCode: "RESERVATION_CONFIRMED_EMAIL",
                channel: NotificationChannel.EMAIL,
                subject: "[SmartParking] Xác nhận đặt chỗ đỗ xe {{reservationId}}",
                bodyContent: "Xin chào, Đặt chỗ đỗ xe tại {{siteName}} cho vị trí {{slotCode}} đã được xác nhận thành công. Thời gian: {{startTime}} - {{endTime}}.",
                version: 1),

            NotificationTemplate.Create(
                templateCode: "PAYMENT_SUCCESS",
                channel: NotificationChannel.IN_APP,
                subject: "Thanh toán thành công",
                bodyContent: "Giao dịch thanh toán {{amount}} {{currency}} qua cổng {{paymentMethod}} đã thành công. Mã đơn hàng: {{paymentOrderId}}.",
                version: 1),

            NotificationTemplate.Create(
                templateCode: "PAYMENT_SUCCESS_EMAIL",
                channel: NotificationChannel.EMAIL,
                subject: "[SmartParking] Biên lai thanh toán thành công đơn hàng {{paymentOrderId}}",
                bodyContent: "Biên lai thanh toán số tiền {{amount}} {{currency}} qua {{paymentMethod}}. Mã giao dịch: {{transactionId}}.",
                version: 1)
        };

        await context.NotificationTemplates.AddRangeAsync(templates, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
