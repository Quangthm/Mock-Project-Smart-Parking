using System.Text.Json;
using System.Text.Json.Serialization;
using SmartParking.PaymentService.Domain.Pricing;

namespace SmartParking.PaymentService.Application.Common.Extensions;

/// <summary>
/// Extension methods for JSON snapshot serialization (DEC-PAY-ARCH-02).
/// Separates serialization logic from the pure Domain layer.
/// </summary>
public static class PricingSnapshotExtensions
{
    private static readonly JsonSerializerOptions SnapshotSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Xuất bản chụp JSON chuẩn hóa (camelCase, enum dạng string) của kết quả tính tiền
    /// để lưu trực tiếp vào cột JSONB invoices.pricing_snapshot (DEC-PAY-ARCH-02).
    /// </summary>
    public static string ToSnapshotJson(this PricingCalculationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return JsonSerializer.Serialize(result, SnapshotSerializerOptions);
    }
}
