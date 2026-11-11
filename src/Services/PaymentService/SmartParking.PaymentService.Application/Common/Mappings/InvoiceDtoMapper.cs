using SmartParking.PaymentService.Application.DTOs.Invoices;
using SmartParking.PaymentService.Domain.Entities;
using SmartParking.PaymentService.Domain.Enums;

namespace SmartParking.PaymentService.Application.Common.Mappings;

public static class InvoiceDtoMapper
{
    public static CustomerInvoiceResponseDto MapToDto(Invoice invoice, PaymentOrder? order = null)
    {
        var snapshot = invoice.PricingSnapshot ?? new InvoicePricingSnapshot();
        var orderCode = !string.IsNullOrEmpty(snapshot.OrderCode) ? snapshot.OrderCode : order?.OrderCode ?? string.Empty;
        var isSettled = snapshot.IsSettled || (order != null && order.Status == PaymentOrderStatus.PAID);
        var status = isSettled ? "PAID" : snapshot.Status;
        var paidAt = snapshot.PaidAt ?? (isSettled ? order?.UpdatedAt : null);
        var pdfUrl = $"/api/v1/payments/invoices/{invoice.Id}/download";

        return new CustomerInvoiceResponseDto(
            InvoiceIdentity: new InvoiceIdentityDto(
                InvoiceId: invoice.Id,
                InvoiceNumber: invoice.InvoiceNumber,
                PaymentOrderId: invoice.PaymentOrderId,
                OrderCode: orderCode,
                IssuedAt: invoice.CreatedAt,
                PdfUrl: pdfUrl
            ),
            CustomerReference: new CustomerReferenceDto(
                AccountId: snapshot.AccountId,
                CustomerType: snapshot.CustomerType,
                DisplayName: snapshot.DisplayName
            ),
            ParkingSessionReference: new ParkingSessionReferenceDto(
                SessionId: invoice.SessionId ?? invoice.ReferenceId,
                ReferenceType: invoice.ReferenceType.ToString(),
                SiteId: invoice.SiteId,
                SiteName: snapshot.SiteName ?? "SmartParking Site",
                VehicleType: snapshot.VehicleType,
                PlateNumber: snapshot.PlateNumber ?? string.Empty
            ),
            Timing: new TimingDto(
                EntryTime: snapshot.EntryTime,
                PricingCutoffAt: snapshot.PricingCutoffAt,
                TotalDurationMinutes: snapshot.TotalDurationMinutes,
                BillableDurationMinutes: snapshot.BillableDurationMinutes
            ),
            TariffPolicy: new TariffPolicyDto(
                TariffId: snapshot.TariffId,
                TariffVersion: snapshot.TariffVersion,
                PolicyName: snapshot.PolicyName,
                FreeParkingGraceMinutes: snapshot.FreeParkingGraceMinutes
            ),
            FinancialSummary: new FinancialSummaryDto(
                SubtotalParkingFee: snapshot.SubtotalParkingFee,
                TotalPenaltyFee: snapshot.TotalPenaltyFee,
                DiscountAmount: snapshot.DiscountAmount,
                FinalTotalAmount: snapshot.FinalTotalAmount > 0 ? snapshot.FinalTotalAmount : invoice.FinalAmount,
                Currency: !string.IsNullOrEmpty(snapshot.Currency) ? snapshot.Currency : "VND",
                DepositDeducted: snapshot.DepositDeducted,
                SurplusRefund: snapshot.SurplusRefund
            ),
            FinancialStatus: new FinancialStatusDto(
                Status: status,
                IsSettled: isSettled,
                PaidAt: paidAt,
                PaymentMethod: snapshot.PaymentMethod,
                TransactionReference: snapshot.TransactionReference,
                GatewayTransactionId: snapshot.GatewayTransactionId
            ),
            LineItems: snapshot.LineItems.Select(item => new InvoiceLineItemDto(
                ItemType: item.ItemType,
                Description: item.Description,
                StartTime: item.StartTime,
                EndTime: item.EndTime,
                DurationMinutes: item.DurationMinutes,
                CoveredMinutes: item.CoveredMinutes,
                ChargeableMinutes: item.ChargeableMinutes,
                UnitRate: item.UnitRate,
                Unit: item.Unit,
                Amount: item.Amount
            )).ToList(),
            PdfUrl: pdfUrl
        );
    }
}
