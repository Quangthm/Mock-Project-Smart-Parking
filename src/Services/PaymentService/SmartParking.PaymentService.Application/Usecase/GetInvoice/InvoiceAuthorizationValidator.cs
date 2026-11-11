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
                "The order is still pending. Wait for the webhook or check the payment status before reading the official invoice.");
        }

        // 2. S2S / Internal Service has full access
        if (actor.IsInternalService)
        {
            return;
        }

        // If not authenticated, throw Unauthorized
        if (!actor.IsAuthenticated)
        {
            throw new PaymentDomainException(PaymentDomainException.Unauthorized, "The authentication token is missing or invalid.");
        }

        // 3. Admin Check: must match tenant
        if (string.Equals(actor.Role, "ADMIN", StringComparison.OrdinalIgnoreCase))
        {
            if (!actor.TenantId.HasValue || actor.TenantId.Value == Guid.Empty || actor.TenantId.Value != invoice.TenantId)
            {
                throw new PaymentDomainException(
                    PaymentDomainException.CrossTenantAccessDenied,
                        "An administrator cannot access invoices belonging to another tenant.");
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
                        "An operator cannot access invoices outside their assigned parking site.");
            }
            return;
        }

        // 5. Driver / Customer Check: must match accountId
        if (string.Equals(actor.Role, "DRIVER", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(actor.Role, "CUSTOMER", StringComparison.OrdinalIgnoreCase))
        {
            var invoiceAccountId = invoice.PricingSnapshot?.AccountId ?? order?.AccountId;
            if (!actor.UserId.HasValue || actor.UserId.Value == Guid.Empty || !invoiceAccountId.HasValue || actor.UserId.Value != invoiceAccountId.Value)
            {
                throw new PaymentDomainException(
                    PaymentDomainException.ForbiddenInvoiceAccess,
                        "A driver cannot access invoices belonging to another account.");
            }
            return;
        }

        // Default Deny: Unrecognized role or insufficient privileges
        throw new PaymentDomainException(
            PaymentDomainException.ForbiddenInvoiceAccess,
            "You are not allowed to access this invoice.");
    }
}
