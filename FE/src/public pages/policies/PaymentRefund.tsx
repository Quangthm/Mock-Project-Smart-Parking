import { UntitledIcon } from "../../components/icon/UntitledIcon"

export function PaymentRefund() {
  return (
    <div
      style={{ maxWidth: "800px", margin: "0 auto", padding: "3rem 1.5rem" }}
    >
      <h1
        style={{
          fontFamily: "Inter, sans-serif",

          fontWeight: 800,

          fontSize: "1.75rem",

          color: "var(--fg)",

          marginBottom: "0.5rem",
        }}
      >
        Chính Sách Thanh Toán và Hoàn Tiền
      </h1>
      <p
        style={{
          color: "var(--muted)",

          fontSize: "0.875rem",

          marginBottom: "2rem",
        }}
      >
        Cập nhật lần cuối: 01/01/2026 | Tuân thủ Luật Bảo vệ người tiêu dùng
        2023 và các quy định thanh toán điện tử Việt Nam
      </p>

      <div
        style={{
          background: "var(--card)",

          border: "2px solid var(--primary)",

          borderRadius: "var(--radius)",

          padding: "1.25rem",

          marginBottom: "2rem",
        }}
      >
        <h3
          style={{
            fontFamily: "Inter, sans-serif",

            fontWeight: 700,

            fontSize: "1rem",

            color: "var(--primary)",

            marginBottom: "0.875rem",
          }}
        >
          <UntitledIcon name="clipboard" size={18} /> Tóm tắt Chính sách Hoàn
          tiền
        </h3>
        <div
          style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}
        >
          {[
            {
              label: "Hủy trước 30 phút",

              value: "Hoàn 100% tiền cọc",

              color: "#22c55e",
            },

            {
              label: "Hủy trước 15–30 phút",

              value: "Hoàn 50% tiền cọc",

              color: "#f59e0b",
            },

            {
              label: "Hủy dưới 15 phút hoặc không đến",

              value: "Không hoàn tiền",

              color: "#ef4444",
            },

            {
              label: "Lỗi kỹ thuật từ SmartParking",

              value: "Hoàn 100% toàn bộ",

              color: "#22c55e",
            },
          ].map((row) => (
            <div
              key={row.label}
              style={{
                display: "flex",

                justifyContent: "space-between",

                alignItems: "center",

                padding: "0.5rem 0.75rem",

                background: `${row.color}10`,

                borderRadius: "6px",

                border: `1px solid ${row.color}30`,
              }}
            >
              <span style={{ fontSize: "0.875rem", color: "var(--fg)" }}>
                {row.label}
              </span>
              <span
                style={{
                  fontWeight: 700,

                  color: row.color,

                  fontSize: "0.875rem",
                }}
              >
                {row.value}
              </span>
            </div>
          ))}
        </div>
      </div>

      <PolicySection title="1. Phương thức thanh toán được chấp nhận">
        <p>
          SmartParking chấp nhận các phương thức thanh toán sau, tất cả đều được
          mã hóa SSL và tuân thủ tiêu chuẩn PCI-DSS:
        </p>
        <ul style={{ paddingLeft: "1.5rem", lineHeight: 2 }}>
          <li>
            <strong>Thẻ quốc tế:</strong> Visa, Mastercard (phí giao dịch: 0%)
          </li>
          <li>
            <strong>Thẻ ATM nội địa:</strong> Tất cả ngân hàng liên kết NAPAS
            (phí: 0%)
          </li>
          <li>
            <strong>Ví điện tử:</strong> MoMo, ZaloPay, VNPAY (phí: 0%)
          </li>
          <li>
            <strong>QR Code:</strong> QR VietQR, ứng dụng ngân hàng (phí: 0%)
          </li>
        </ul>
        <p>
          SmartParking không thu phụ phí giao dịch từ người dùng. Phí liên ngân
          hàng (nếu có) do đơn vị phát hành thẻ thu.
        </p>
      </PolicySection>

      <PolicySection title="2. Cơ chế đặt cọc">
        <p>
          Khi xác nhận booking, Tài xế thanh toán tiền cọc bằng 30% tổng phí đỗ
          xe. Phần còn lại được thu khi Check-out.
        </p>
        <p>
          <strong>Mục đích:</strong> Đảm bảo cam kết giữ chỗ và bù đắp doanh thu
          mất mát cho Chủ bãi trong trường hợp hủy muộn.
        </p>
      </PolicySection>

      <PolicySection title="3. Quy trình hoàn tiền">
        <p>
          <strong>Thời gian xử lý hoàn tiền:</strong>
        </p>
        <ul style={{ paddingLeft: "1.5rem", lineHeight: 2 }}>
          <li>Ví điện tử (MoMo, ZaloPay, VNPAY): 1–2 giờ</li>
          <li>Thẻ ngân hàng nội địa: 1–3 ngày làm việc</li>
          <li>Thẻ quốc tế (Visa/Mastercard): 5–7 ngày làm việc</li>
        </ul>
        <p>
          Tiền hoàn về chính phương thức thanh toán ban đầu. Không hỗ trợ hoàn
          về tài khoản khác.
        </p>
      </PolicySection>

      <PolicySection title="4. Xác minh OTP trong giao dịch">
        <p>
          Mọi giao dịch đặt chỗ đều yêu cầu xác minh OTP 6 chữ số gửi qua SMS
          hoặc email trong vòng 5 phút. Nhập sai 3 lần liên tiếp sẽ:
        </p>
        <ul style={{ paddingLeft: "1.5rem", lineHeight: 1.8 }}>
          <li>Hủy booking tự động</li>
          <li>Hoàn toàn bộ tiền cọc (xem là lỗi kỹ thuật)</li>
          <li>Tạm khóa tài khoản 5 phút</li>
        </ul>
      </PolicySection>

      <PolicySection title="5. Tranh chấp và khiếu nại thanh toán">
        <p>Nếu bạn tin rằng có sai sót trong giao dịch, vui lòng:</p>
        <ul style={{ paddingLeft: "1.5rem", lineHeight: 2 }}>
          <li>Gửi khiếu nại trong vòng 7 ngày kể từ ngày giao dịch</li>
          <li>
            Qua email: <strong>support.smartparkingvn@gmail.com</strong>
          </li>
          <li>
            Hotline: <strong>1900-xxxx</strong> (8:00–22:00 hằng ngày)
          </li>
          <li>Kèm theo mã booking và bằng chứng giao dịch</li>
        </ul>
        <p>
          SmartParking cam kết giải quyết trong vòng 5 ngày làm việc. Trường hợp
          phức tạp tối đa 15 ngày làm việc theo Luật Bảo vệ người tiêu dùng
          2023.
        </p>
      </PolicySection>

      <PolicySection title="6. Chính sách đối với Chủ bãi (Revenue Share)">
        <p>
          Phí nền tảng: <strong>15%</strong> trên mỗi giao dịch booking hoàn
          thành. Thanh toán tháng vào ngày 5 của tháng tiếp theo qua chuyển
          khoản ngân hàng. Chủ bãi nhận hóa đơn điện tử theo Nghị định
          123/2020/NĐ-CP.
        </p>
      </PolicySection>

      <PolicySection title="7. Cơ sở pháp lý">
        <p>Chính sách này tuân thủ:</p>
        <ul style={{ paddingLeft: "1.5rem", lineHeight: 2 }}>
          <li>Luật Bảo vệ người tiêu dùng 2023 (Luật số 19/2023/QH15)</li>
          <li>Nghị định 52/2013/NĐ-CP về thương mại điện tử (sửa đổi 2021)</li>
          <li>Thông tư 39/2018/TT-NHNN về hoạt động trung gian thanh toán</li>
          <li>Nghị định 101/2012/NĐ-CP về thanh toán không dùng tiền mặt</li>
        </ul>
      </PolicySection>
    </div>
  )
}

function PolicySection({
  title,

  children,
}: {
  title: string

  children: React.ReactNode
}) {
  return (
    <section style={{ marginBottom: "2rem" }}>
      <h2
        style={{
          fontFamily: "Inter, sans-serif",

          fontWeight: 700,

          fontSize: "1.05rem",

          color: "var(--fg)",

          marginBottom: "0.75rem",

          borderLeft: "3px solid var(--primary)",

          paddingLeft: "0.75rem",
        }}
      >
        {title}
      </h2>
      <div
        style={{
          fontFamily: "Inter, sans-serif",

          fontSize: "0.9rem",

          color: "var(--muted)",

          lineHeight: 1.8,
        }}
      >
        {children}
      </div>
    </section>
  )
}
