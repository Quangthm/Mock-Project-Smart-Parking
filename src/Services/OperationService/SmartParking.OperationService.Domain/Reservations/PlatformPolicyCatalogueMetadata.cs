namespace SmartParking.OperationService.Domain.Reservations;

public sealed record PolicyCatalogueBound(int Minimum, int Maximum);

public sealed record PolicyCatalogueMetadata(string Key, string Name, string Description, string DataType,
    string Unit, int PlatformDefault, bool IsOverridableByOwner, PolicyCatalogueBound? DefaultBound);

public static class PlatformPolicyCatalogueMetadata
{
    public static IReadOnlyList<PolicyCatalogueMetadata> All { get; } = Array.AsReadOnly(new[]
    {
        new PolicyCatalogueMetadata("RESERVATION_MIN_DURATION", "Minimum Reservation Duration",
            "Khoảng thời gian đặt chỗ tối thiểu cho phép trong một phiên đặt.", "duration", "minutes", 15, true, null),
        new PolicyCatalogueMetadata("RESERVATION_MAX_DURATION", "Maximum Reservation Duration",
            "Khoảng thời gian đặt chỗ tối đa cho phép trong một phiên đặt thông thường.", "duration", "minutes", 1440, true, null),
        new PolicyCatalogueMetadata("MAX_ADVANCE_BOOKING", "Maximum Advance Booking Horizon",
            "Khoảng thời gian tối đa mà tài xế có thể đặt chỗ trước ngày giờ bắt đầu.", "duration", "minutes", 10080, true, null),
        new PolicyCatalogueMetadata("INTER_BOOKING_BUFFER", "Inter-booking Buffer",
            "Khoảng đệm tối thiểu giữa hai lượt đặt chỗ liên tiếp trên cùng một vị trí. 0 là hợp lệ (cho phép liền kề).", "duration", "minutes", 0, true, null),
        new PolicyCatalogueMetadata("PAYMENT_HOLD_DURATION", "Payment Hold Duration",
            "Thời gian giữ chỗ tạm thời chờ người dùng hoàn tất thanh toán tiền cọc.", "duration", "minutes", 5, true, null),
        new PolicyCatalogueMetadata("RESERVATION_PROTECTION_WINDOW", "Reservation Protection Window",
            "Cửa sổ bảo vệ chỗ trước giờ bắt đầu đặt; chặn các yêu cầu phân bổ khác chiếm chỗ.", "duration", "minutes", 240, true, null),
        new PolicyCatalogueMetadata("ALLOCATION_LEAD_TIME", "Allocation Lead Time",
            "Thời gian hệ thống thực hiện giải thuật phân bổ slot cụ thể trước giờ bắt đầu.", "duration", "minutes", 60, true, null),
        new PolicyCatalogueMetadata("FACILITY_FAILURE_REFUND_LEAD_TIME", "Facility Failure Refund Lead Time",
            "Mốc thời gian trước giờ bắt đầu mà nếu bãi xe gặp sự cố, tài xế được quyền nhận hoàn tiền 100%.", "duration", "minutes", 15, true, null),
        new PolicyCatalogueMetadata("OCCUPANCY_VALIDITY_TIMESPAN", "Occupancy Validity Timespan",
            "Khoảng thời gian hiệu lực tối đa của bằng chứng cảm biến chiếm chỗ trước khi coi là hết hạn.", "duration", "minutes", 240, true, null),
        new PolicyCatalogueMetadata("ARRIVAL_LATE_TOLERANCE", "Arrival Late Tolerance",
            "Thời gian gia hạn đến muộn sau giờ đặt chỗ trước khi bị đánh dấu vắng mặt (No-show).", "duration", "minutes", 15, true,
            new PolicyCatalogueBound(15, 30)),
        new PolicyCatalogueMetadata("RAPID_CANCELLATION_COUNT", "Rapid Cancellation Threshold Count",
            "Số lượt hủy đặt chỗ liên tiếp trong cửa sổ ngắn để kích hoạt cơ chế giới hạn tài khoản.", "count", "submissions", 2, false, null),
        new PolicyCatalogueMetadata("RAPID_CANCELLATION_WINDOW", "Rapid Cancellation Window",
            "Cửa sổ thời gian lăn dùng để tính số lượt hủy đặt chỗ liên tiếp.", "duration", "minutes", 15, false, null),
        new PolicyCatalogueMetadata("RAPID_CANCELLATION_RESTRICTION_DURATION", "Rapid Cancellation Restriction Duration",
            "Thời gian tạm khóa quyền tạo đặt chỗ mới của tài khoản sau khi vi phạm hành vi hủy liên tục.", "duration", "minutes", 180, false, null),
        new PolicyCatalogueMetadata("DAILY_CANCELLATION_LIMIT", "Daily Cancellation Limit",
            "Giới hạn số lượt hủy đặt chỗ tối đa trong một ngày (tính theo múi giờ Asia/Ho_Chi_Minh).", "count", "submissions", 5, false, null)
    });
}