export function TermsOfService() {
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
        Điều Khoản Sử Dụng Dịch Vụ
      </h1>
      <p
        style={{
          color: "var(--muted)",
          fontSize: "0.875rem",
          marginBottom: "2rem",
        }}
      >
        Cập nhật lần cuối: 01/01/2026 | Effective Date: January 1, 2026
      </p>

      <PolicySection title="1. Giới thiệu">
        <p>
          SmartParking (sau đây gọi là "Nền tảng", "Chúng tôi") cung cấp dịch vụ
          tìm kiếm, đặt chỗ và thanh toán bãi đỗ xe trực tuyến tại Việt Nam.
          Bằng việc sử dụng dịch vụ, bạn đồng ý tuân thủ các điều khoản dưới đây
          theo quy định của pháp luật Việt Nam, bao gồm Luật Giao dịch điện tử
          2023, Luật Bảo vệ người tiêu dùng 2023 và Nghị định 13/2023/NĐ-CP về
          bảo vệ dữ liệu cá nhân.
        </p>
      </PolicySection>

      <PolicySection title="2. Đối tượng sử dụng">
        <p>
          <strong>Tài xế (Driver):</strong> Cá nhân từ 18 tuổi trở lên có phương
          tiện hợp lệ và tài khoản đã xác thực.
        </p>
        <p>
          <strong>Chủ bãi (Owner):</strong> Tổ chức hoặc cá nhân có tư cách pháp
          lý sở hữu hoặc quản lý bãi đỗ xe hợp pháp tại Việt Nam.
        </p>
        <p>
          <strong>Vận hành (Operator):</strong> Nhân viên được Chủ bãi ủy quyền
          quản lý vận hành bãi đỗ xe.
        </p>
        <p>
          <strong>Quản trị viên (Admin):</strong> Nhân viên nội bộ SmartParking
          có tài khoản hệ thống được cung cấp sẵn.
        </p>
      </PolicySection>

      <PolicySection title="3. Đăng ký tài khoản và bảo mật">
        <p>
          Người dùng có trách nhiệm: (a) Cung cấp thông tin chính xác khi đăng
          ký; (b) Bảo mật mật khẩu và không chia sẻ với bên thứ ba; (c) Thông
          báo ngay cho SmartParking nếu phát hiện truy cập trái phép. Mật khẩu
          phải đáp ứng yêu cầu độ bảo mật tối thiểu (8–15 ký tự, gồm chữ hoa,
          chữ thường, số và ký tự đặc biệt).
        </p>
      </PolicySection>

      <PolicySection title="4. Điều khoản sử dụng dịch vụ đặt chỗ">
        <p>
          (a) Tài xế cam kết cung cấp biển số xe chính xác và hợp lệ theo Thông
          tư 79/2024/TT-BCA.
        </p>
        <p>
          (b) Mỗi booking được xác nhận bằng mã OTP. Nhập sai 3 lần, booking bị
          hủy và tài khoản tạm khóa 5 phút.
        </p>
        <p>
          (c) Thời gian ân hạn được áp dụng theo chính sách của từng Chủ bãi.
        </p>
        <p>
          (d) SmartParking không chịu trách nhiệm về mất mát, hư hỏng tài sản
          trong bãi đỗ ngoài phạm vi hợp đồng với Chủ bãi.
        </p>
      </PolicySection>

      <PolicySection title="5. Biển số xe và xác minh phương tiện">
        <p>
          Theo Luật Trật tự, An toàn giao thông đường bộ 2024 và Thông tư
          79/2024/TT-BCA, hệ thống SmartParking nhận diện:
        </p>
        <ul style={{ paddingLeft: "1.5rem", lineHeight: 2 }}>
          <li>
            Biển số xe Việt Nam: theo định dạng XX[A-Z]-NNNNN (ví dụ: 51A-12345,
            51AB-67890)
          </li>
          <li>
            Biển số xe nước ngoài: được đánh dấu riêng và cần Operator xác minh
            thủ công
          </li>
          <li>Xe điện: biển số đặc biệt theo Thông tư 79/2024/TT-BCA</li>
        </ul>
      </PolicySection>

      <PolicySection title="6. Trách nhiệm của các bên">
        <p>
          <strong>SmartParking:</strong> Duy trì hệ thống hoạt động ổn định; xử
          lý khiếu nại trong vòng 5 ngày làm việc; bảo vệ dữ liệu cá nhân theo
          quy định.
        </p>
        <p>
          <strong>Chủ bãi:</strong> Duy trì chất lượng và an toàn bãi đỗ; cung
          cấp thông tin chính xác; tuân thủ chính sách hoàn tiền của nền tảng.
        </p>
        <p>
          <strong>Tài xế:</strong> Đỗ xe đúng vị trí được đặt; rời khỏi đúng
          giờ; tuân thủ nội quy bãi đỗ.
        </p>
      </PolicySection>

      <PolicySection title="7. Giải quyết tranh chấp">
        <p>
          Mọi tranh chấp phát sinh từ việc sử dụng dịch vụ SmartParking sẽ được
          giải quyết theo pháp luật Việt Nam. Trước tiên, các bên nỗ lực hòa
          giải trong vòng 30 ngày. Nếu không thành, tranh chấp được đưa ra Tòa
          án nhân dân có thẩm quyền tại Thành phố Hồ Chí Minh.
        </p>
      </PolicySection>

      <PolicySection title="8. Hiệu lực và thay đổi">
        <p>
          SmartParking có quyền cập nhật Điều khoản này và sẽ thông báo trước 15
          ngày qua email. Việc tiếp tục sử dụng dịch vụ sau thời điểm thay đổi
          có hiệu lực được xem là đồng ý với các điều khoản mới.
        </p>
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
