import { useState, useEffect } from "react"
import type { ParkingLot, LotPolicyPdf } from "../../lib/types"
import { store } from "../../lib/store"
import { UntitledIcon } from "../icon/UntitledIcon"

interface LotPolicyModalProps {
  lot: ParkingLot
  onClose: () => void
}

export function LotPolicyModal({ lot, onClose }: LotPolicyModalProps) {
  const [policyPdf, setPolicyPdf] = useState<LotPolicyPdf | null>(() => {
    return store.getLotPolicyPdf(lot.id)
  })

  const [activeTab, setActiveTab] = useState<"document" | "summary">("document")

  useEffect(() => {
    const fresh = store.getLotPolicyPdf(lot.id)
    if (fresh) setPolicyPdf(fresh)
  }, [lot.id])

  const documentTitle =
    policyPdf?.title || `Quy định & Chính sách Vận hành Bãi đỗ: ${lot.name}`
  const fileName =
    policyPdf?.fileName ||
    `Chinh_Sach_Bai_Do_${lot.name.replace(/\s+/g, "_")}.pdf`
  const hasUploadedPdf = Boolean(
    policyPdf?.fileData &&
      policyPdf.fileData.startsWith("data:application/pdf"),
  )

  // Fallback printable simulated document data URL if no custom file uploaded
  const downloadUrl = hasUploadedPdf
    ? policyPdf!.fileData
    : `data:text/plain;charset=utf-8,${encodeURIComponent(
        `SMARTPARK - CHÍNH SÁCH VẬN HÀNH BÃI ĐỖ XE\n\n` +
          `Cơ sở: ${lot.name}\n` +
          `Địa chỉ: ${lot.address}\n` +
          `Loại hình: ${lot.type}\n` +
          `Ngày cập nhật: ${new Date(policyPdf?.uploadedAt || lot.createdAt).toLocaleDateString("vi-VN")}\n\n` +
          `1. GIỜ HOẠT ĐỘNG VÀ BIỂU PHÍ:\n` +
          `- Biểu phí ban ngày: ${lot.hourlyRate.toLocaleString("vi-VN")} ₫/giờ\n` +
          `- Biểu phí ban đêm: ${lot.nightRate.toLocaleString("vi-VN")} ₫/giờ\n` +
          `- Thời gian ân hạn miễn phí: ${lot.gracePeriodMinutes || 15} phút đầu tiên khi vào bãi\n\n` +
          `2. QUY ĐỊNH BẢO VỆ ĐẶT CHỖ (SRS v0.9):\n` +
          `- Khung giờ bảo vệ đặt chỗ (Reservation Protection Window): 60 phút tính từ thời điểm đặt hẹn\n` +
          `- Thời gian xử lý điều phối (Allocation Lead Time): Tối đa 15 phút trước giờ nhận chỗ\n` +
          `- Tỷ lệ đặt cọc giữ chỗ: 20% trên tổng chi phí dự tính\n\n` +
          `3. CHÍNH SÁCH HỦY VÀ HOÀN TIỀN:\n` +
          `- Hủy trước 30 phút so với giờ hẹn: Hoàn 100% tiền cọc về ví tài xế\n` +
          `- Quá giờ hẹn sau thời gian bảo vệ mà không vào bãi: Hệ thống chuyển trạng thái NO_SHOW và giải phóng vị trí đỗ\n\n` +
          `4. AN TOÀN VÀ TRÁCH NHIỆM:\n` +
          `- Phương tiện phải tuân thủ biển báo hướng dẫn và giới hạn tốc độ 10 km/h trong khuôn viên\n` +
          `- Không để tài sản quý giá trong xe. Bãi đỗ xe không chịu trách nhiệm với mất mát tư trang cá nhân không khai báo.`,
      )}`

  return (
    <div
      style={{
        position: "fixed",
        inset: 0,
        zIndex: 999,
        background: "rgba(0, 0, 0, 0.7)",
        backdropFilter: "blur(4px)",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: "1rem",
      }}
      role="dialog"
      aria-modal="true"
      aria-labelledby="policy-modal-title"
    >
      <div
        className="card animate-in"
        style={{
          width: "100%",
          maxWidth: "760px",
          maxHeight: "90vh",
          display: "flex",
          flexDirection: "column",
          gap: "1rem",
          padding: "1.25rem 1.5rem",
          overflow: "hidden",
          borderRadius: "0.85rem",
        }}
      >
        {/* Header */}
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "flex-start",
            borderBottom: "1px solid var(--border)",
            paddingBottom: "0.85rem",
          }}
        >
          <div
            style={{ display: "flex", alignItems: "center", gap: "0.75rem" }}
          >
            <div
              style={{
                width: 40,
                height: 40,
                borderRadius: "8px",
                background: "rgba(37, 99, 235, 0.12)",
                color: "var(--primary)",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                flexShrink: 0,
              }}
            >
              <UntitledIcon name="file" size={22} />
            </div>
            <div>
              <div
                style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}
              >
                <h3
                  id="policy-modal-title"
                  style={{
                    fontFamily: "Outfit",
                    fontWeight: 700,
                    fontSize: "1.15rem",
                    margin: 0,
                    color: "var(--fg)",
                  }}
                >
                  Chính sách & Quy định Bãi đỗ
                </h3>
                <span
                  style={{
                    fontSize: "0.72rem",
                    fontWeight: 600,
                    padding: "0.15rem 0.5rem",
                    borderRadius: "4px",
                    background: hasUploadedPdf
                      ? "rgba(34, 197, 94, 0.15)"
                      : "rgba(59, 130, 246, 0.15)",
                    color: hasUploadedPdf ? "#16a34a" : "var(--primary)",
                  }}
                >
                  {hasUploadedPdf ? "File PDF Chính Thức" : "Tài liệu Chuẩn"}
                </span>
              </div>
              <p
                style={{
                  margin: "0.2rem 0 0",
                  fontSize: "0.8rem",
                  color: "var(--muted)",
                }}
              >
                {lot.name} · {lot.address}
              </p>
            </div>
          </div>

          <button
            type="button"
            onClick={onClose}
            aria-label="Đóng"
            style={{
              background: "none",
              border: "none",
              color: "var(--muted)",
              cursor: "pointer",
              padding: "4px",
              display: "flex",
            }}
          >
            <UntitledIcon name="x" size={20} />
          </button>
        </div>

        {/* Tab Controls */}
        <div
          style={{
            display: "flex",
            gap: "0.5rem",
            borderBottom: "1px solid var(--border)",
            paddingBottom: "0.5rem",
          }}
        >
          <button
            type="button"
            className={activeTab === "document" ? "btn-primary" : "btn-outline"}
            onClick={() => setActiveTab("document")}
            style={{ padding: "0.35rem 0.8rem", fontSize: "0.8rem" }}
          >
            <UntitledIcon name="file" size={14} /> Văn Bản Quy Định (PDF)
          </button>
          <button
            type="button"
            className={activeTab === "summary" ? "btn-primary" : "btn-outline"}
            onClick={() => setActiveTab("summary")}
            style={{ padding: "0.35rem 0.8rem", fontSize: "0.8rem" }}
          >
            <UntitledIcon name="check" size={14} /> Tóm Tắt Quy Chuẩn Nhanh
          </button>
        </div>

        {/* Content Body */}
        <div
          style={{
            flex: 1,
            overflowY: "auto",
            display: "flex",
            flexDirection: "column",
            gap: "1rem",
            paddingRight: "0.25rem",
          }}
        >
          {activeTab === "document" ? (
            hasUploadedPdf ? (
              <div
                style={{
                  display: "flex",
                  flexDirection: "column",
                  gap: "0.75rem",
                }}
              >
                <div
                  style={{
                    display: "flex",
                    justifyContent: "space-between",
                    alignItems: "center",
                    fontSize: "0.8rem",
                    color: "var(--muted)",
                  }}
                >
                  <span>
                    Tệp đính kèm:{" "}
                    <strong style={{ color: "var(--fg)" }}>{fileName}</strong> (
                    {policyPdf?.fileSize || "PDF"})
                  </span>
                  <span>
                    Cập nhật:{" "}
                    {new Date(
                      policyPdf?.uploadedAt || Date.now(),
                    ).toLocaleDateString("vi-VN")}
                  </span>
                </div>
                <iframe
                  src={policyPdf?.fileData}
                  title="Tài liệu chính sách bãi xe"
                  style={{
                    width: "100%",
                    height: "440px",
                    border: "1px solid var(--border)",
                    borderRadius: "0.5rem",
                    background: "#ffffff",
                  }}
                />
              </div>
            ) : (
              /* Professional Structured Document View */
              <div
                style={{
                  background: "var(--bg)",
                  border: "1px solid var(--border)",
                  borderRadius: "0.5rem",
                  padding: "1.5rem",
                  fontFamily: "'Inter', sans-serif",
                  fontSize: "0.85rem",
                  lineHeight: 1.6,
                  color: "var(--fg)",
                  boxShadow: "inset 0 1px 2px rgba(0,0,0,0.1)",
                }}
              >
                <div
                  style={{
                    textAlign: "center",
                    borderBottom: "1.5px double var(--border)",
                    paddingBottom: "1rem",
                    marginBottom: "1.25rem",
                  }}
                >
                  <p
                    style={{
                      margin: 0,
                      fontSize: "0.75rem",
                      textTransform: "uppercase",
                      letterSpacing: "1px",
                      color: "var(--muted)",
                    }}
                  >
                    HỆ THỐNG QUẢN LÝ BÃI ĐỖ THÔNG MINH SMARTPARK
                  </p>
                  <h4
                    style={{
                      margin: "0.35rem 0",
                      fontSize: "1.2rem",
                      fontWeight: 700,
                      color: "var(--fg)",
                    }}
                  >
                    {documentTitle}
                  </h4>
                  <p
                    style={{
                      margin: 0,
                      fontSize: "0.78rem",
                      color: "var(--muted)",
                    }}
                  >
                    Cơ sở: <strong>{lot.name}</strong> · Địa chỉ: {lot.address}
                  </p>
                </div>

                <div
                  style={{
                    display: "flex",
                    flexDirection: "column",
                    gap: "1rem",
                  }}
                >
                  <div>
                    <h5
                      style={{
                        margin: "0 0 0.35rem",
                        fontSize: "0.92rem",
                        fontWeight: 700,
                        color: "var(--primary)",
                      }}
                    >
                      Điều 1. Khung giờ hoạt động & Biểu giá dịch vụ
                    </h5>
                    <p style={{ margin: 0 }}>
                      - Bãi xe phục vụ 24/7 đối với tất cả phương tiện đăng ký
                      hợp lệ qua hệ thống SmartPark.
                      <br />- Đơn giá ban ngày (06:00 - 22:00):{" "}
                      <strong>
                        {lot.hourlyRate.toLocaleString("vi-VN")} VNĐ/giờ
                      </strong>
                      .<br />- Đơn giá ban đêm (22:00 - 06:00 hôm sau):{" "}
                      <strong>
                        {lot.nightRate.toLocaleString("vi-VN")} VNĐ/giờ
                      </strong>
                      .<br />- Thời gian ân hạn (Grace Period): Miễn phí{" "}
                      <strong>{lot.gracePeriodMinutes || 15} phút</strong> đầu
                      tiên khi xe vào bãi làm thủ tục hoặc quay đầu.
                    </p>
                  </div>

                  <div>
                    <h5
                      style={{
                        margin: "0 0 0.35rem",
                        fontSize: "0.92rem",
                        fontWeight: 700,
                        color: "var(--primary)",
                      }}
                    >
                      Điều 2. Quy định Đặt chỗ & Khung bảo vệ chỗ đỗ
                      (Reservation Protection Window)
                    </h5>
                    <p style={{ margin: 0 }}>
                      - Khung giờ bảo vệ đặt chỗ (
                      <strong>Reservation Protection Window</strong>): Bãi xe
                      cam kết bảo lưu và giữ nguyên vị trí đỗ trong vòng{" "}
                      <strong>60 phút</strong> tính từ thời điểm đặt hẹn.
                      <br />- Tỷ lệ đặt cọc: Tài xế thanh toán trước{" "}
                      <strong>20%</strong> giá trị dự tính để kích hoạt trạng
                      thái giữ chỗ.
                      <br />- Thời gian xử lý điều phối (Allocation Lead Time):
                      Hệ thống chốt phân bổ cố định trước giờ vào bãi tối thiểu
                      15 phút.
                    </p>
                  </div>

                  <div>
                    <h5
                      style={{
                        margin: "0 0 0.35rem",
                        fontSize: "0.92rem",
                        fontWeight: 700,
                        color: "var(--primary)",
                      }}
                    >
                      Điều 3. Chính sách Hủy chỗ & Xử lý Quá giờ (No-show
                      Policy)
                    </h5>
                    <p style={{ margin: 0 }}>
                      - Hủy trước giờ hẹn từ 30 phút trở lên: Hoàn lại{" "}
                      <strong>100%</strong> tiền cọc về tài khoản tài xế.
                      <br />- Hủy trong vòng 30 phút trước giờ hẹn: Khấu trừ phí
                      đặt chỗ tương đương 1 giờ lưu xe.
                      <br />- Quá 60 phút bảo vệ không vào bãi (No-show): Chỗ đỗ
                      tự động được giải phóng để phục vụ tài xế vãng lai; tiền
                      cọc không được hoàn lại.
                    </p>
                  </div>

                  <div>
                    <h5
                      style={{
                        margin: "0 0 0.35rem",
                        fontSize: "0.92rem",
                        fontWeight: 700,
                        color: "var(--primary)",
                      }}
                    >
                      Điều 4. An toàn giao thông & Trách nhiệm phương tiện
                    </h5>
                    <p style={{ margin: 0 }}>
                      - Tài xế tuân thủ biển báo, vạch kẻ chỉ dẫn và tốc độ tối
                      đa không quá 10 km/h trong bãi đỗ.
                      <br />- Tự bảo quản tài sản có giá trị trong xe. Mọi sự cố
                      kỹ thuật hoặc tranh chấp vui lòng liên hệ Ban Quản lý bãi
                      đỗ để trích xuất camera giám sát.
                    </p>
                  </div>
                </div>
              </div>
            )
          ) : (
            /* Summary Grid */
            <div
              style={{
                display: "grid",
                gridTemplateColumns: "repeat(auto-fit, minmax(220px, 1fr))",
                gap: "0.75rem",
              }}
            >
              <div
                style={{
                  background: "var(--bg)",
                  border: "1px solid var(--border)",
                  borderRadius: "0.5rem",
                  padding: "0.85rem",
                }}
              >
                <div
                  style={{
                    color: "var(--muted)",
                    fontSize: "0.75rem",
                    marginBottom: "0.25rem",
                  }}
                >
                  Mức giá ban ngày
                </div>
                <div
                  style={{
                    fontSize: "1.1rem",
                    fontWeight: 700,
                    color: "var(--fg)",
                  }}
                >
                  {lot.hourlyRate.toLocaleString("vi-VN")} ₫/h
                </div>
                <div
                  style={{
                    fontSize: "0.72rem",
                    color: "var(--muted)",
                    marginTop: "0.2rem",
                  }}
                >
                  Khung giờ 06:00 – 22:00
                </div>
              </div>

              <div
                style={{
                  background: "var(--bg)",
                  border: "1px solid var(--border)",
                  borderRadius: "0.5rem",
                  padding: "0.85rem",
                }}
              >
                <div
                  style={{
                    color: "var(--muted)",
                    fontSize: "0.75rem",
                    marginBottom: "0.25rem",
                  }}
                >
                  Mức giá ban đêm
                </div>
                <div
                  style={{
                    fontSize: "1.1rem",
                    fontWeight: 700,
                    color: "var(--fg)",
                  }}
                >
                  {lot.nightRate.toLocaleString("vi-VN")} ₫/h
                </div>
                <div
                  style={{
                    fontSize: "0.72rem",
                    color: "var(--muted)",
                    marginTop: "0.2rem",
                  }}
                >
                  Khung giờ 22:00 – 06:00
                </div>
              </div>

              <div
                style={{
                  background: "var(--bg)",
                  border: "1px solid var(--border)",
                  borderRadius: "0.5rem",
                  padding: "0.85rem",
                }}
              >
                <div
                  style={{
                    color: "var(--muted)",
                    fontSize: "0.75rem",
                    marginBottom: "0.25rem",
                  }}
                >
                  Bảo vệ giữ chỗ (SRS)
                </div>
                <div
                  style={{
                    fontSize: "1.1rem",
                    fontWeight: 700,
                    color: "#818cf8",
                  }}
                >
                  60 Phút
                </div>
                <div
                  style={{
                    fontSize: "0.72rem",
                    color: "var(--muted)",
                    marginTop: "0.2rem",
                  }}
                >
                  Reservation Protection Window
                </div>
              </div>

              <div
                style={{
                  background: "var(--bg)",
                  border: "1px solid var(--border)",
                  borderRadius: "0.5rem",
                  padding: "0.85rem",
                }}
              >
                <div
                  style={{
                    color: "var(--muted)",
                    fontSize: "0.75rem",
                    marginBottom: "0.25rem",
                  }}
                >
                  Thời gian ân hạn
                </div>
                <div
                  style={{
                    fontSize: "1.1rem",
                    fontWeight: 700,
                    color: "#22c55e",
                  }}
                >
                  {lot.gracePeriodMinutes || 15} Phút
                </div>
                <div
                  style={{
                    fontSize: "0.72rem",
                    color: "var(--muted)",
                    marginTop: "0.2rem",
                  }}
                >
                  Miễn phí vào và rời bãi
                </div>
              </div>

              <div
                style={{
                  background: "var(--bg)",
                  border: "1px solid var(--border)",
                  borderRadius: "0.5rem",
                  padding: "0.85rem",
                  gridColumn: "1 / -1",
                }}
              >
                <div
                  style={{
                    color: "var(--muted)",
                    fontSize: "0.75rem",
                    marginBottom: "0.25rem",
                  }}
                >
                  Chính sách cọc & Hoàn tiền
                </div>
                <div
                  style={{
                    fontSize: "0.85rem",
                    color: "var(--fg)",
                    lineHeight: 1.5,
                  }}
                >
                  ✓ Đặt cọc <strong>20%</strong> để xác nhận giữ chỗ.
                  <br />✓ Hoàn trả <strong>100%</strong> tiền cọc khi hủy trước
                  giờ hẹn 30 phút.
                  <br />✓ Hỗ trợ xử lý điều chuyển tự động nếu vị trí gặp sự cố
                  bảo trì đột xuất.
                </div>
              </div>
            </div>
          )}
        </div>

        {/* Footer Actions */}
        <div
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            borderTop: "1px solid var(--border)",
            paddingTop: "0.75rem",
            flexWrap: "wrap",
            gap: "0.5rem",
          }}
        >
          <a
            href={downloadUrl}
            download={fileName}
            className="btn-primary"
            style={{
              textDecoration: "none",
              fontSize: "0.84rem",
              padding: "0.45rem 0.9rem",
              display: "inline-flex",
              alignItems: "center",
              gap: "0.4rem",
            }}
          >
            <UntitledIcon name="download" size={15} /> Tải xuống Văn Bản PDF
          </a>

          <button
            type="button"
            className="btn-outline"
            onClick={onClose}
            style={{ fontSize: "0.84rem", padding: "0.45rem 1rem" }}
          >
            Đóng
          </button>
        </div>
      </div>
    </div>
  )
}
