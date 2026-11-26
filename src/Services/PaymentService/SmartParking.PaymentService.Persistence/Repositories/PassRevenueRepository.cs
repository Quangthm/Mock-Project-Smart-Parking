using Microsoft.EntityFrameworkCore;
using SmartParking.PaymentService.Application.Common.Interfaces.Persistence;
using SmartParking.PaymentService.Application.DTOs.PassPlans;
using SmartParking.PaymentService.Domain.Enums;

namespace SmartParking.PaymentService.Persistence.Repositories;

public sealed class PassRevenueRepository(PaymentDbContext db) : IPassRevenueRepository
{
    public async Task<PassRevenueSummaryDto> GetSitePassRevenueSummaryAsync(
        Guid siteId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default)
    {
        var ordersQuery = db.PaymentOrders
            .AsNoTracking()
            .Where(o => o.SiteId == siteId &&
                        o.OrderType == OrderType.MONTHLY_PASS_FEE &&
                        (o.Status == PaymentOrderStatus.PAID || o.Status == PaymentOrderStatus.REFUNDED));

        if (from.HasValue)
        {
            ordersQuery = ordersQuery.Where(o => o.CreatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            ordersQuery = ordersQuery.Where(o => o.CreatedAt <= to.Value);
        }

        var orders = await ordersQuery
            .Select(o => new { o.Id, o.FinalAmount, o.Status })
            .ToArrayAsync(ct);

        if (orders.Length == 0)
        {
            return new PassRevenueSummaryDto(
                siteId,
                "VND",
                0m,
                0m,
                0m,
                0m,
                0m,
                0m,
                new PassRevenueCountsDto(0, 0, 0));
        }

        var orderIds = orders.Select(o => o.Id).ToArray();

        var refundRequests = await db.RefundRequests
            .AsNoTracking()
            .Where(r => orderIds.Contains(r.PaymentOrderId))
            .Select(r => new { r.PaymentOrderId, r.Amount, r.Status })
            .ToArrayAsync(ct);

        var refundTransactions = await db.PaymentTransactions
            .AsNoTracking()
            .Where(t => orderIds.Contains(t.PaymentOrderId) && t.TransactionType == TransactionType.REFUND)
            .Select(t => new { t.PaymentOrderId, t.Amount, t.Status })
            .ToArrayAsync(ct);

        decimal collectedAmount = 0m;
        decimal collectedGrantedAmount = 0m;
        decimal collectedAwaitingRefundAmount = 0m;
        decimal pendingRefundAmount = 0m;
        decimal refundedAmount = 0m;
        var fulfilledPasses = 0;
        var awaitingRefundOrders = 0;
        var completedRefunds = 0;

        foreach (var order in orders)
        {
            var orderRequests = refundRequests.Where(r => r.PaymentOrderId == order.Id).ToArray();
            var orderRefundTxs = refundTransactions.Where(t => t.PaymentOrderId == order.Id).ToArray();

            var orderPendingRefund = orderRequests
                .Where(r => r.Status is "REFUND_REQUESTED" or "REFUND_APPROVED" or "REFUND_SUBMITTED"
                    or "PENDING" or "APPROVED" or "PROCESSING")
                .Sum(r => r.Amount);

            var completedTx = orderRefundTxs
                .Where(t => t.Status == PaymentStatus.SUCCESS)
                .ToArray();

            var completedReq = orderRequests
                .Where(r => r.Status is "REFUND_COMPLETED" or "COMPLETED" or "REFUNDED")
                .ToArray();

            decimal orderRefunded;
            int orderCompletedCount;
            if (completedTx.Length > 0)
            {
                orderRefunded = completedTx.Sum(t => t.Amount);
                orderCompletedCount = Math.Max(completedTx.Length, completedReq.Length);
            }
            else if (completedReq.Length > 0)
            {
                orderRefunded = completedReq.Sum(r => r.Amount);
                orderCompletedCount = completedReq.Length;
            }
            else if (order.Status == PaymentOrderStatus.REFUNDED)
            {
                orderRefunded = order.FinalAmount;
                orderCompletedCount = 1;
            }
            else
            {
                orderRefunded = 0m;
                orderCompletedCount = 0;
            }

            var orderAwaiting = Math.Min(order.FinalAmount, orderPendingRefund);
            var orderGranted = order.FinalAmount - orderAwaiting;

            collectedAmount += order.FinalAmount;
            collectedGrantedAmount += orderGranted;
            collectedAwaitingRefundAmount += orderAwaiting;
            pendingRefundAmount += orderPendingRefund;
            refundedAmount += orderRefunded;
            completedRefunds += orderCompletedCount;

            if (orderPendingRefund > 0m)
            {
                awaitingRefundOrders++;
            }

            if (orderAwaiting < order.FinalAmount &&
                !(order.Status == PaymentOrderStatus.REFUNDED && orderRefunded >= order.FinalAmount))
            {
                fulfilledPasses++;
            }
        }

        var netSettledRevenue = collectedGrantedAmount - refundedAmount;

        return new PassRevenueSummaryDto(
            siteId,
            "VND",
            collectedAmount,
            collectedGrantedAmount,
            collectedAwaitingRefundAmount,
            pendingRefundAmount,
            refundedAmount,
            netSettledRevenue,
            new PassRevenueCountsDto(fulfilledPasses, awaitingRefundOrders, completedRefunds));
    }
}
