export function PrivacyPolicy() {
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
        Chính Sách Bảo Mật Thông Tin
      </h1>
      <p
        style={{
          color: "var(--muted)",
          fontSize: "0.875rem",
          marginBottom: "2rem",
        }}
      >
        Cập nhật lần cuối: 01/01/2026 | Tuân thủ Nghị định 13/2023/NĐ-CP về bảo
        vệ dữ liệu cá nhân
      </p>

      <PolicySection title="1. Dữ liệu chúng tôi thu thập">
        <p>
          <strong>Thông tin định danh:</strong> Họ tên, địa chỉ email, số điện
          thoại, ảnh đại diện.
        </p>
        <p>
          <strong>Thông tin phương tiện:</strong> Biển số xe, loại phương tiện,
          hình ảnh biển số.
        </p>
        <p>
          <strong>Dữ liệu giao dịch:</strong> Lịch sử đặt chỗ, phương thức thanh
          toán, hóa đơn.
        </p>
        <p>
          <strong>Dữ liệu vị trí:</strong> Vị trí tìm kiếm bãi đỗ (chỉ khi người
          dùng cho phép).
        </p>
        <p>
          <strong>Dữ liệu thiết bị:</strong> Địa chỉ IP, loại trình duyệt, thời
          gian truy cập.
        </p>
      </PolicySection>

      <PolicySection title="2. Mục đích thu thập và xử lý dữ liệu">
        <ul style={{ paddingLeft: "1.5rem", lineHeight: 2 }}>
          <li>Cung cấp và cải thiện dịch vụ đặt chỗ đỗ xe</li>
          <li>Xác thực danh tính và ngăn chặn gian lận</li>
          <li>Xử lý thanh toán và hoàn tiền</li>
          <li>Gửi thông báo giao dịch qua SMS và email</li>
          <li>Phân tích và cải thiện trải nghiệm người dùng</li>
          <li>Tuân thủ nghĩa vụ pháp lý theo pháp luật Việt Nam</li>
        </ul>
      </PolicySection>

      <PolicySection title="3. Căn cứ pháp lý xử lý dữ liệu">
        <p>
          Theo Nghị định 13/2023/NĐ-CP, SmartParking xử lý dữ liệu cá nhân dựa
          trên:
        </p>
        <p>(a) Sự đồng ý của chủ thể dữ liệu khi đăng ký tài khoản;</p>
        <p>(b) Thực hiện hợp đồng dịch vụ giữa người dùng và SmartParking;</p>
        <p>(c) Tuân thủ nghĩa vụ pháp lý theo các quy định hiện hành.</p>
      </PolicySection>

      <PolicySection title="4. Lưu trữ và bảo mật dữ liệu">
        <p>
          Dữ liệu được lưu trữ trên máy chủ đặt tại Việt Nam, được mã hóa theo
          tiêu chuẩn AES-256. Chúng tôi áp dụng các biện pháp bảo mật kỹ thuật
          và tổ chức phù hợp để ngăn chặn truy cập trái phép.
        </p>
        <p>
          <strong>Thời gian lưu trữ:</strong> Dữ liệu tài khoản được lưu trong
          suốt thời gian hoạt động và tối đa 5 năm sau khi xóa tài khoản theo
          yêu cầu pháp lý.
        </p>
      </PolicySection>

      <PolicySection title="5. Chia sẻ dữ liệu với bên thứ ba">
        <p>SmartParking chỉ chia sẻ dữ liệu cá nhân trong các trường hợp:</p>
        <ul style={{ paddingLeft: "1.5rem", lineHeight: 2 }}>
          <li>
            Chủ bãi đỗ xe: biển số xe và thông tin booking để xác nhận đỗ xe
          </li>
          <li>
            Cổng thanh toán (VNPAY, MoMo, ZaloPay, v.v.): xử lý giao dịch tài
            chính
          </li>
          <li>Cơ quan nhà nước: khi có yêu cầu pháp lý hợp lệ</li>
          <li>
            Không bán dữ liệu cá nhân cho bên thứ ba với mục đích thương mại
          </li>
        </ul>
      </PolicySection>

      <PolicySection title="6. Quyền của chủ thể dữ liệu">
        <p>Theo Nghị định 13/2023/NĐ-CP, bạn có quyền:</p>
        <ul style={{ paddingLeft: "1.5rem", lineHeight: 2 }}>
          <li>
            <strong>Quyền được biết:</strong> Biết thông tin về việc xử lý dữ
            liệu cá nhân của mình
          </li>
          <li>
            <strong>Quyền đồng ý:</strong> Đồng ý hoặc không đồng ý cho phép xử
            lý dữ liệu
          </li>
          <li>
            <strong>Quyền truy cập:</strong> Xem và yêu cầu bản sao dữ liệu cá
            nhân
          </li>
          <li>
            <strong>Quyền chỉnh sửa:</strong> Yêu cầu chỉnh sửa dữ liệu không
            chính xác
          </li>
          <li>
            <strong>Quyền xóa dữ liệu:</strong> Yêu cầu xóa dữ liệu trong phạm
            vi pháp luật cho phép
          </li>
          <li>
            <strong>Quyền hạn chế:</strong> Yêu cầu hạn chế xử lý dữ liệu trong
            một số trường hợp
          </li>
          <li>
            <strong>Quyền phản đối:</strong> Phản đối việc xử lý dữ liệu cho mục
            đích tiếp thị
          </li>
        </ul>
        <p>
          Gửi yêu cầu về quyền dữ liệu qua email:{" "}
          <strong>support@smartparking.vn</strong>
        </p>
      </PolicySection>

      <PolicySection title="7. Cookies và công nghệ theo dõi">
        <p>
          SmartParking sử dụng cookies thiết yếu để duy trì phiên đăng nhập và
          cookies phân tích (có thể tắt) để cải thiện dịch vụ. Bạn có thể quản
          lý cookies trong cài đặt trình duyệt.
        </p>
      </PolicySection>

      <PolicySection title="8. Liên hệ về bảo mật dữ liệu">
        <p>
          Cán bộ bảo vệ dữ liệu : <strong>support@smartparking.vn</strong>
        </p>
        <p>Địa chỉ: Tòa nhà Smart Hub, 17 Lê Duẩn, Quận 1, TP. Hồ Chí Minh</p>
        <p>
          Trong trường hợp vi phạm dữ liệu nghiêm trọng, SmartParking sẽ thông
          báo đến Bộ Công an (Cục An ninh mạng và phòng, chống tội phạm sử dụng
          công nghệ cao) và người dùng bị ảnh hưởng trong vòng 72 giờ theo quy
          định.
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
