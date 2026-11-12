using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SmartParking.PaymentService.Application.Common.Interfaces;
using SmartParking.PaymentService.Application.DTOs.Invoices;

namespace SmartParking.PaymentService.Infrastructure.Adapters;

public class QuestPdfInvoiceGenerator : IPdfInvoiceGenerator
{
    private static readonly CultureInfo ViCulture = new("vi-VN");
    private static readonly TimeSpan IctOffset = TimeSpan.FromHours(7);

    public Task<byte[]> GenerateInvoicePdfAsync(CustomerInvoiceResponseDto invoice, CancellationToken cancellationToken = default)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(32);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(9.5f).FontColor(Colors.Grey.Darken4));

                page.Header().Element(header => ComposeHeader(header, invoice));
                page.Content().Element(content => ComposeContent(content, invoice));
                page.Footer().Element(footer => ComposeFooter(footer, invoice));
            });
        });

        var pdfBytes = document.GeneratePdf();
        return Task.FromResult(pdfBytes);
    }

    private static void ComposeHeader(IContainer container, CustomerInvoiceResponseDto invoice)
    {
        var siteName = !string.IsNullOrWhiteSpace(invoice.ParkingSessionReference.SiteName)
            ? invoice.ParkingSessionReference.SiteName
            : "Bãi đỗ xe thông minh SmartParking";

        var issuedAtIct = invoice.InvoiceIdentity.IssuedAt.ToOffset(IctOffset);

        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(brandCol =>
                {
                    brandCol.Item().Text("SMARTPARKING").Bold().FontSize(18).FontColor(Colors.Blue.Darken3);
                    brandCol.Item().Text($"Bãi xe: {siteName}").FontSize(11).SemiBold();
                    brandCol.Item().Text("Hệ thống quản lý đỗ xe thông minh toàn diện").FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                });

                row.ConstantItem(220).AlignRight().Column(metaCol =>
                {
                    metaCol.Item().Text("HÓA ĐƠN ĐIỆN TỬ").Bold().FontSize(14).FontColor(Colors.Blue.Darken2);
                    metaCol.Item().Text($"Số HĐ: {invoice.InvoiceIdentity.InvoiceNumber}").SemiBold();
                    metaCol.Item().Text($"Mã đơn: {invoice.InvoiceIdentity.OrderCode}");
                    metaCol.Item().Text($"Ngày lập: {issuedAtIct:dd/MM/yyyy HH:mm:ss} (ICT)");
                });
            });

            col.Item().PaddingTop(10).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
        });
    }

    private static void ComposeContent(IContainer container, CustomerInvoiceResponseDto invoice)
    {
        var entryIct = invoice.Timing.EntryTime.ToOffset(IctOffset);
        var exitIct = invoice.Timing.PricingCutoffAt.ToOffset(IctOffset);

        var durationHours = invoice.Timing.TotalDurationMinutes / 60;
        var durationMins = invoice.Timing.TotalDurationMinutes % 60;
        var durationText = durationHours > 0
            ? $"{invoice.Timing.TotalDurationMinutes} phút ({durationHours}h {durationMins}m)"
            : $"{invoice.Timing.TotalDurationMinutes} phút";

        var customerName = !string.IsNullOrWhiteSpace(invoice.CustomerReference.DisplayName)
            ? invoice.CustomerReference.DisplayName
            : (invoice.CustomerReference.AccountId.HasValue ? "Khách hàng đăng ký" : "Khách vãng lai");

        container.PaddingVertical(10).Column(col =>
        {
            // 1. Info Card
            col.Item().Background(Colors.Grey.Lighten4).Padding(12).Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("THÔNG TIN KHÁCH HÀNG & PHƯƠNG TIỆN").Bold().FontSize(10).FontColor(Colors.Blue.Darken3);
                    c.Item().PaddingTop(3).Text($"Khách hàng: {customerName}");
                    c.Item().Text($"Biển số xe: {invoice.ParkingSessionReference.PlateNumber}");
                    c.Item().Text($"Loại phương tiện: {invoice.ParkingSessionReference.VehicleType}");
                    if (!string.IsNullOrWhiteSpace(invoice.TariffPolicy.PolicyName))
                    {
                        c.Item().Text($"Chính sách cước: {invoice.TariffPolicy.PolicyName}");
                    }
                });

                row.RelativeItem().Column(c =>
                {
                    c.Item().Text("THÔNG TIN LƯỢT ĐỖ XE").Bold().FontSize(10).FontColor(Colors.Blue.Darken3);
                    c.Item().PaddingTop(3).Text($"Giờ vào: {entryIct:dd/MM/yyyy HH:mm:ss}");
                    c.Item().Text($"Giờ ra: {exitIct:dd/MM/yyyy HH:mm:ss}");
                    c.Item().Text($"Tổng thời lượng: {durationText}");
                    c.Item().Text($"Thời lượng tính cước: {invoice.Timing.BillableDurationMinutes} phút");
                });
            });

            // 2. Line Items Section
            col.Item().PaddingTop(14).Text("CHI TIẾT CƯỚC PHÍ").Bold().FontSize(10).FontColor(Colors.Blue.Darken3);

            col.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(28);
                    columns.RelativeColumn(3);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(2);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(1.5f);
                    columns.RelativeColumn(2);
                });

                table.Header(header =>
                {
                    header.Cell().Background(Colors.Blue.Darken2).Padding(5).Text("#").FontColor(Colors.White).SemiBold();
                    header.Cell().Background(Colors.Blue.Darken2).Padding(5).Text("Khoản mục").FontColor(Colors.White).SemiBold();
                    header.Cell().Background(Colors.Blue.Darken2).Padding(5).Text("Bắt đầu").FontColor(Colors.White).SemiBold();
                    header.Cell().Background(Colors.Blue.Darken2).Padding(5).Text("Kết thúc").FontColor(Colors.White).SemiBold();
                    header.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignCenter().Text("Số phút").FontColor(Colors.White).SemiBold();
                    header.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignRight().Text("Đơn giá").FontColor(Colors.White).SemiBold();
                    header.Cell().Background(Colors.Blue.Darken2).Padding(5).AlignRight().Text("Thành tiền").FontColor(Colors.White).SemiBold();
                });

                if (invoice.LineItems != null && invoice.LineItems.Count > 0)
                {
                    var index = 1;
                    foreach (var item in invoice.LineItems)
                    {
                        var startText = item.StartTime.HasValue ? item.StartTime.Value.ToOffset(IctOffset).ToString("HH:mm dd/MM") : "-";
                        var endText = item.EndTime.HasValue ? item.EndTime.Value.ToOffset(IctOffset).ToString("HH:mm dd/MM") : "-";

                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(index.ToString());
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(item.Description);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(startText);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(endText);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).AlignCenter().Text($"{item.ChargeableMinutes}");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).AlignRight().Text(FormatCurrency(item.UnitRate));
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).AlignRight().Text(FormatCurrency(item.Amount));
                        index++;
                    }
                }
                else
                {
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text("1");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text("Cước phí dịch vụ đỗ xe tiêu chuẩn");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(entryIct.ToString("HH:mm dd/MM"));
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).Text(exitIct.ToString("HH:mm dd/MM"));
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).AlignCenter().Text($"{invoice.Timing.BillableDurationMinutes}");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).AlignRight().Text("-");
                    table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(5).AlignRight().Text(FormatCurrency(invoice.FinancialSummary.SubtotalParkingFee));
                }
            });

            // 3. Financial Summary
            col.Item().PaddingTop(12).AlignRight().Width(300).Column(fin =>
            {
                fin.Item().Row(r =>
                {
                    r.RelativeItem().Text("Tiền cước gốc:");
                    r.ConstantItem(120).AlignRight().Text(FormatCurrency(invoice.FinancialSummary.SubtotalParkingFee));
                });

                if (invoice.FinancialSummary.TotalPenaltyFee > 0)
                {
                    fin.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Tiền phạt quá giờ / vi phạm:").FontColor(Colors.Red.Darken2);
                        r.ConstantItem(120).AlignRight().Text(FormatCurrency(invoice.FinancialSummary.TotalPenaltyFee)).FontColor(Colors.Red.Darken2);
                    });
                }

                if (invoice.FinancialSummary.DiscountAmount > 0)
                {
                    fin.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Giảm giá / Khuyến mãi:");
                        r.ConstantItem(120).AlignRight().Text($"-{FormatCurrency(invoice.FinancialSummary.DiscountAmount)}");
                    });
                }

                if (invoice.FinancialSummary.DepositDeducted > 0)
                {
                    fin.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Khấu trừ tiền cọc trước:");
                        r.ConstantItem(120).AlignRight().Text($"-{FormatCurrency(invoice.FinancialSummary.DepositDeducted)}");
                    });
                }

                if (invoice.FinancialSummary.SurplusRefund > 0)
                {
                    fin.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Tiền thừa hoàn trả:").FontColor(Colors.Green.Darken2);
                        r.ConstantItem(120).AlignRight().Text($"-{FormatCurrency(invoice.FinancialSummary.SurplusRefund)}").FontColor(Colors.Green.Darken2);
                    });
                }

                fin.Item().PaddingVertical(4).LineHorizontal(1).LineColor(Colors.Grey.Darken1);

                fin.Item().Row(r =>
                {
                    r.RelativeItem().Text("TỔNG TIỀN ĐÃ THANH TOÁN:").Bold().FontSize(11).FontColor(Colors.Blue.Darken3);
                    r.ConstantItem(140).AlignRight().Text(FormatCurrency(invoice.FinancialSummary.FinalTotalAmount)).Bold().FontSize(12).FontColor(Colors.Blue.Darken3);
                });

                fin.Item().PaddingTop(4).Row(r =>
                {
                    r.RelativeItem().Text("Trạng thái hóa đơn:").SemiBold();
                    r.ConstantItem(140).AlignRight().Text(invoice.FinancialStatus.IsSettled ? "ĐÃ THANH TOÁN (PAID)" : invoice.FinancialStatus.Status).SemiBold().FontColor(invoice.FinancialStatus.IsSettled ? Colors.Green.Darken2 : Colors.Orange.Darken2);
                });

                if (!string.IsNullOrWhiteSpace(invoice.FinancialStatus.PaymentMethod))
                {
                    fin.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Phương thức thanh toán:").FontSize(8.5f);
                        r.ConstantItem(140).AlignRight().Text(invoice.FinancialStatus.PaymentMethod).FontSize(8.5f);
                    });
                }

                if (!string.IsNullOrWhiteSpace(invoice.FinancialStatus.TransactionReference) || !string.IsNullOrWhiteSpace(invoice.FinancialStatus.GatewayTransactionId))
                {
                    var refCode = !string.IsNullOrWhiteSpace(invoice.FinancialStatus.GatewayTransactionId)
                        ? invoice.FinancialStatus.GatewayTransactionId
                        : invoice.FinancialStatus.TransactionReference;

                    fin.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Mã giao dịch / đối soát:").FontSize(8.5f);
                        r.ConstantItem(140).AlignRight().Text(refCode!).FontSize(8.5f);
                    });
                }
            });
        });
    }

    private static void ComposeFooter(IContainer container, CustomerInvoiceResponseDto invoice)
    {
        container.Column(foot =>
        {
            foot.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
            foot.Item().PaddingTop(6).Row(row =>
            {
                row.RelativeItem().Text("Hóa đơn điện tử khởi tạo từ hệ thống SmartParking. Để tra cứu thông tin hoặc khiếu nại, vui lòng liên hệ ban quản lý bãi xe.").FontSize(8).FontColor(Colors.Grey.Darken1);
                row.ConstantItem(90).AlignRight().Text(x =>
                {
                    x.Span("Trang ");
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        });
    }

    private static string FormatCurrency(decimal amount)
    {
        return amount.ToString("N0", ViCulture) + " ₫";
    }
}
