using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;
using SmartParking.PaymentService.Domain.Exceptions;

namespace SmartParking.PaymentService.Application.Usecase.GetInvoice;

public static class InvoiceAuthorizationValidator
{
    public static void ValidateAccess(Invoice invoice, PaymentOrder? order, ICurrentActor actor)
    {
        // 1. Settlement check: Section 7 error catalog
        var isSettled = invoice.PricingSnapshot?.IsSettled == true || (order != null && order.Status == PaymentOrderStatus.PAID);
        if (!isSettled)
        {
            throw new PaymentDomainException(
                PaymentDomainException.UnsettledPaymentOrder,
                "Yêu cầu đọc hóa đơn chính thức khi đơn hàng vẫn đang PENDING. Tiếp tục chờ Webhook hoặc tra cứu trạng thái thanh toán.");
        }

        // 2. S2S / Internal Service has full access
        if (actor.IsInternalService)
        {
            return;
        }

        // If not authenticated, throw Unauthorized
        if (!actor.IsAuthenticated)
        {
            throw new PaymentDomainException(PaymentDomainException.Unauthorized, "Thiếu token xác thực hoặc token không hợp lệ.");
        }

        // 3. Admin Check: must match tenant
        if (string.Equals(actor.Role, "ADMIN", StringComparison.OrdinalIgnoreCase))
        {
            if (!actor.TenantId.HasValue || actor.TenantId.Value == Guid.Empty || actor.TenantId.Value != invoice.TenantId)
            {
                throw new PaymentDomainException(
                    PaymentDomainException.CrossTenantAccessDenied,
                    "Quản trị viên không có quyền truy cập hóa đơn của tenant khác.");
            }
            return;
        }

        // 4. Operator Check: must match site
        if (string.Equals(actor.Role, "OPERATOR", StringComparison.OrdinalIgnoreCase))
        {
            if (!actor.SiteId.HasValue || actor.SiteId.Value == Guid.Empty || actor.SiteId.Value != invoice.SiteId)
            {
                throw new PaymentDomainException(
                    PaymentDomainException.OperatorSiteMismatch,
                    "Nhân viên cổng không có quyền truy cập hóa đơn ngoài bãi trực được phân công.");
            }
            return;
        }

        // 5. Driver Check: must match accountId
        if (string.Equals(actor.Role, "DRIVER", StringComparison.OrdinalIgnoreCase))
        {
            var invoiceAccountId = invoice.PricingSnapshot?.AccountId ?? order?.AccountId;
            if (!actor.UserId.HasValue || actor.UserId.Value == Guid.Empty || !invoiceAccountId.HasValue || actor.UserId.Value != invoiceAccountId.Value)
            {
                throw new PaymentDomainException(
                    PaymentDomainException.ForbiddenInvoiceAccess,
                    "Tài xế không có quyền truy cập hóa đơn của tài khoản khác.");
            }
            return;
        }

        // Default Deny: Unrecognized role or insufficient privileges
        throw new PaymentDomainException(
            PaymentDomainException.ForbiddenInvoiceAccess,
            "Người dùng không có quyền truy cập hóa đơn này.");
    }
}
