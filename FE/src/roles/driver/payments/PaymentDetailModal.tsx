import { useState } from "react"
import { UntitledIcon } from "../../../components/icon/UntitledIcon"
import type { PaymentTransactionRecord } from "../../../lib/structureTypes"
import { PaymentMethodLogo } from "../../../components/payment/PaymentMethodLogo"

interface PaymentDetailModalProps {
  transaction: PaymentTransactionRecord | null
  onClose: () => void
  onRetryPayment: (
    transactionId: string,
    newMethod: PaymentTransactionRecord["paymentMethod"],
  ) => void
}

export function PaymentDetailModal({
  transaction,
  onClose,
  onRetryPayment,
}: PaymentDetailModalProps) {
  const [isRetrying, setIsRetrying] = useState(false)
  const [selectedMethod, setSelectedMethod] =
    useState<PaymentTransactionRecord["paymentMethod"]>("qr")
  const [isProcessing, setIsProcessing] = useState(false)
  const [copied, setCopied] = useState(false)

  if (!transaction) return null

  const handleCopy = (text: string) => {
    navigator.clipboard?.writeText(text)
    setCopied(true)
    setTimeout(() => setCopied(false), 2000)
  }

  const handleExecuteRetry = () => {
    setIsProcessing(true)
    setTimeout(() => {
      onRetryPayment(transaction.id, selectedMethod)
      setIsProcessing(false)
      setIsRetrying(false)
    }, 600)
  }

  const getMethodLabel = (
    method: PaymentTransactionRecord["paymentMethod"],
  ) => {
    switch (method) {
      case "qr":
        return "VietQR Bank Transfer"
      case "momo":
        return "MoMo E-Wallet"
      case "vnpay":
        return "VNPAY Gateway"
      case "visa":
        return "Credit / Debit Card (Visa/Mastercard)"
      case "applepay":
        return "Apple Pay"
      case "zalopay":
        return "ZaloPay E-Wallet"
      default:
        return method
    }
  }

  // Itemized Pricing Breakdown calculation (standard Vietnam VAT 8%)
  const totalAmount = transaction.amount
  const subtotalBeforeVat = Math.round(totalAmount / 1.08)
  const vatAmount = totalAmount - subtotalBeforeVat
  const facilityFee = Math.round(subtotalBeforeVat * 0.82)
  const platformFee = subtotalBeforeVat - facilityFee

  const isPaid = transaction.paymentStatus === "paid"
  const isPending = transaction.paymentStatus === "pending"
  const isFailed = transaction.paymentStatus === "failed"
  const isRefunded = transaction.paymentStatus === "refunded"

  const formattedDate = new Date(transaction.createdAt).toLocaleString(
    "vi-VN",
    {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    },
  )

  return (
    <div
      role="presentation"
      className="receipt-print-wrapper"
      onMouseDown={(e) => {
        if (e.target === e.currentTarget) onClose()
      }}
      style={{
        position: "fixed",
        inset: 0,
        zIndex: 1000,
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        padding: "1.25rem",
        background: "rgba(2, 6, 23, 0.75)",
        backdropFilter: "blur(5px)",
      }}
    >
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="payment-receipt-title"
        style={{
          width: "100%",
          maxWidth: 680,
          background: "var(--card)",
          color: "var(--fg)",
          border: "1px solid var(--border)",
          borderRadius: "1rem",
          boxShadow: "0 25px 60px -15px rgba(0, 0, 0, 0.5)",
          padding: "1.25rem",
          maxHeight: "92vh",
          overflowY: "auto",
          display: "flex",
          flexDirection: "column",
          gap: "1rem",
        }}
      >
        {/* Top Controls Toolbar (Hidden in Print) */}
        <div
          className="no-print"
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            paddingBottom: "0.75rem",
            borderBottom: "1px solid var(--border)",
          }}
        >
          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
            <span style={{ color: "var(--primary)", display: "flex" }}>
              <UntitledIcon name="file" size={18} />
            </span>
            <span
              id="payment-receipt-title"
              style={{
                fontFamily: "Outfit",
                fontWeight: 700,
                fontSize: "1.1rem",
                color: "var(--fg)",
              }}
            >
              Driver Payment Receipt
            </span>
            <span
              style={{
                fontSize: "0.7rem",
                fontWeight: 700,
                padding: "0.15rem 0.45rem",
                borderRadius: "999px",
                background: isPaid
                  ? "#22c55e20"
                  : isFailed
                    ? "#ef444420"
                    : "#f59e0b20",
                color: isPaid ? "#16a34a" : isFailed ? "#dc2626" : "#d97706",
              }}
            >
              {transaction.paymentStatus.toUpperCase()}
            </span>
          </div>

          <div style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}>
            <button
              type="button"
              className="btn-primary"
              style={{
                fontSize: "0.82rem",
                padding: "0.4rem 0.85rem",
                background: "var(--primary)",
                color: "var(--primary-fg)",
                display: "inline-flex",
                alignItems: "center",
                gap: "0.35rem",
              }}
              onClick={() => window.print()}
            >
              <UntitledIcon name="file" size={14} /> Print / Export PDF
            </button>

            <button
              type="button"
              aria-label="Close modal"
              onClick={onClose}
              style={{
                width: 32,
                height: 32,
                borderRadius: "50%",
                border: "1px solid var(--border)",
                background: "var(--bg)",
                color: "var(--fg)",
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                cursor: "pointer",
              }}
            >
              <UntitledIcon name="x" size={16} />
            </button>
          </div>
        </div>

        {/* Failure & Retry banner for failed transactions (Hidden in Print) */}
        {isFailed && (
          <div
            className="no-print"
            style={{
              padding: "0.875rem 1rem",
              borderRadius: "0.625rem",
              background: "#ef444415",
              border: "1px solid #ef444440",
            }}
          >
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: "0.5rem",
                color: "#dc2626",
                marginBottom: "0.25rem",
              }}
            >
              <UntitledIcon name="alert" size={16} />
              <strong style={{ fontSize: "0.85rem" }}>
                Payment Unsuccessful
              </strong>
            </div>
            <p
              style={{
                margin: "0 0 0.75rem",
                fontSize: "0.8rem",
                color: "var(--fg)",
                lineHeight: 1.45,
              }}
            >
              {transaction.failureReason ||
                "Transaction was declined by provider or timed out. Please retry with a valid payment method."}
            </p>

            {isRetrying ? (
              <div
                style={{
                  display: "grid",
                  gap: "0.65rem",
                  paddingTop: "0.5rem",
                  borderTop: "1px solid #ef444430",
                }}
              >
                <label style={{ fontSize: "0.78rem", fontWeight: 600 }}>
                  Select Payment Method to Retry:
                </label>
                <div
                  style={{
                    display: "grid",
                    gridTemplateColumns: "repeat(auto-fit, minmax(140px, 1fr))",
                    gap: "0.4rem",
                  }}
                >
                  {[
                    { id: "qr", label: "VietQR / Banking" },
                    { id: "momo", label: "MoMo Wallet" },
                    { id: "vnpay", label: "VNPay Gateway" },
                    { id: "visa", label: "Visa / Mastercard" },
                    { id: "applepay", label: "Apple Pay" },
                    { id: "zalopay", label: "ZaloPay" },
                  ].map((method) => (
                    <button
                      key={method.id}
                      type="button"
                      onClick={() => setSelectedMethod(method.id as any)}
                      style={{
                        padding: "0.45rem 0.55rem",
                        borderRadius: "var(--radius)",
                        border: `1.5px solid ${
                          selectedMethod === method.id
                            ? "var(--primary)"
                            : "var(--border)"
                        }`,
                        background:
                          selectedMethod === method.id
                            ? "color-mix(in srgb, var(--primary) 12%, var(--bg))"
                            : "var(--bg)",
                        color: "var(--fg)",
                        fontSize: "0.78rem",
                        fontWeight: 600,
                        cursor: "pointer",
                        display: "flex",
                        alignItems: "center",
                        gap: "0.4rem",
                      }}
                    >
                      <PaymentMethodLogo method={method.id} size="xs" />
                      <span
                        style={{
                          whiteSpace: "nowrap",
                          overflow: "hidden",
                          textOverflow: "ellipsis",
                        }}
                      >
                        {method.label}
                      </span>
                    </button>
                  ))}
                </div>
                <div
                  style={{
                    display: "flex",
                    justifyContent: "flex-end",
                    gap: "0.5rem",
                    marginTop: "0.25rem",
                  }}
                >
                  <button
                    type="button"
                    className="btn-outline"
                    style={{ fontSize: "0.78rem", padding: "0.3rem 0.7rem" }}
                    onClick={() => setIsRetrying(false)}
                  >
                    Cancel
                  </button>
                  <button
                    type="button"
                    className="btn-primary"
                    disabled={isProcessing}
                    style={{ fontSize: "0.78rem", padding: "0.3rem 0.85rem" }}
                    onClick={handleExecuteRetry}
                  >
                    {isProcessing ? "Processing..." : "Confirm & Pay Now"}
                  </button>
                </div>
              </div>
            ) : (
              <button
                type="button"
                className="btn-primary"
                style={{ fontSize: "0.8rem", padding: "0.35rem 0.85rem" }}
                onClick={() => setIsRetrying(true)}
              >
                <UntitledIcon name="banknote" size={14} /> Retry Payment Now
              </button>
            )}
          </div>
        )}

        {/* OFFICIAL BUSINESS RECEIPT DOCUMENT (Paper-styled white background for export/print) */}
        <div
          className="receipt-document"
          style={{
            background: "#ffffff",
            color: "#111827",
            borderRadius: "0.75rem",
            border: "1px solid #e5e7eb",
            padding: "2rem 1.75rem",
            boxShadow: "0 4px 20px rgba(0, 0, 0, 0.08)",
            position: "relative",
            fontFamily:
              'Inter, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif',
          }}
        >
          {/* VISUAL STATUS STAMP (SRS 11 - Only Paid transactions receive official PAID seal) */}
          {isPaid && (
            <div
              style={{
                position: "absolute",
                top: "2.5rem",
                right: "2rem",
                border: "3px solid #16a34a",
                color: "#16a34a",
                padding: "0.35rem 0.85rem",
                borderRadius: "6px",
                fontWeight: 900,
                fontSize: "1rem",
                letterSpacing: "0.12em",
                textTransform: "uppercase",
                transform: "rotate(-7deg)",
                userSelect: "none",
                opacity: 0.88,
                textAlign: "center",
                lineHeight: 1.2,
                boxShadow: "inset 0 0 0 1px #16a34a",
              }}
            >
              PAID
              <div
                style={{
                  fontSize: "0.58rem",
                  fontWeight: 700,
                  letterSpacing: "0.04em",
                }}
              >
                ĐÃ THANH TOÁN
              </div>
            </div>
          )}

          {isPending && (
            <div
              style={{
                position: "absolute",
                top: "2.5rem",
                right: "2rem",
                border: "2px dashed #ca8a04",
                color: "#ca8a04",
                padding: "0.35rem 0.75rem",
                borderRadius: "6px",
                fontWeight: 800,
                fontSize: "0.85rem",
                letterSpacing: "0.08em",
                textTransform: "uppercase",
                transform: "rotate(-4deg)",
                userSelect: "none",
                opacity: 0.85,
                textAlign: "center",
                lineHeight: 1.2,
              }}
            >
              PENDING
              <div style={{ fontSize: "0.55rem", fontWeight: 600 }}>
                CHỜ THANH TOÁN
              </div>
            </div>
          )}

          {isFailed && (
            <div
              style={{
                position: "absolute",
                top: "2.5rem",
                right: "2rem",
                border: "3px solid #dc2626",
                color: "#dc2626",
                padding: "0.35rem 0.75rem",
                borderRadius: "6px",
                fontWeight: 800,
                fontSize: "0.85rem",
                letterSpacing: "0.08em",
                textTransform: "uppercase",
                transform: "rotate(5deg)",
                userSelect: "none",
                opacity: 0.85,
                textAlign: "center",
                lineHeight: 1.2,
              }}
            >
              DECLINED
              <div style={{ fontSize: "0.55rem", fontWeight: 600 }}>
                THẤT BẠI
              </div>
            </div>
          )}

          {isRefunded && (
            <div
              style={{
                position: "absolute",
                top: "2.5rem",
                right: "2rem",
                border: "3px solid #7c3aed",
                color: "#7c3aed",
                padding: "0.35rem 0.75rem",
                borderRadius: "6px",
                fontWeight: 800,
                fontSize: "0.85rem",
                letterSpacing: "0.08em",
                textTransform: "uppercase",
                transform: "rotate(-4deg)",
                userSelect: "none",
                opacity: 0.85,
                textAlign: "center",
                lineHeight: 1.2,
              }}
            >
              REFUNDED
              <div style={{ fontSize: "0.55rem", fontWeight: 600 }}>
                ĐÃ HOÀN TIỀN
              </div>
            </div>
          )}

          {/* Header & SmartParking Branding */}
          <div
            style={{
              display: "flex",
              alignItems: "flex-start",
              justifyContent: "space-between",
              paddingBottom: "1.25rem",
              borderBottom: "2px solid #111827",
            }}
          >
            <div>
              <div
                style={{ display: "flex", alignItems: "center", gap: "0.5rem" }}
              >
                <div
                  style={{
                    width: 28,
                    height: 28,
                    borderRadius: "6px",
                    background: "#2563eb",
                    color: "#ffffff",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    fontWeight: 900,
                    fontSize: "1rem",
                  }}
                >
                  P
                </div>
                <span
                  style={{
                    fontSize: "1.25rem",
                    fontWeight: 800,
                    color: "#111827",
                    letterSpacing: "-0.02em",
                  }}
                >
                  SmartPark Intelligent Parking System
                </span>
              </div>
              <p
                style={{
                  margin: "0.25rem 0 0",
                  fontSize: "0.75rem",
                  color: "#4b5563",
                  lineHeight: 1.4,
                }}
              >
                SmartPark Vietnam Co., Ltd. · Tax ID: 0318924510
                <br />
                Customer Support: 1900 6868 · Email: support@smartpark.vn
              </p>
            </div>
          </div>

          {/* Document Title Bar */}
          <div style={{ padding: "1rem 0 0.85rem", textAlign: "center" }}>
            <h2
              style={{
                fontSize: "1.15rem",
                fontWeight: 800,
                letterSpacing: "0.04em",
                margin: 0,
                color: "#111827",
                textTransform: "uppercase",
              }}
            >
              OFFICIAL ELECTRONIC RECEIPT / BIÊN LAI ĐIỆN TỬ
            </h2>
            <div
              style={{
                fontSize: "0.74rem",
                color: "#6b7280",
                marginTop: "0.2rem",
              }}
            >
              Receipt No:{" "}
              <strong style={{ fontFamily: "monospace", color: "#111827" }}>
                {transaction.id}
              </strong>{" "}
              · Issued on: {formattedDate}
            </div>
          </div>

          {/* Primary Meta Information Grid */}
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(2, 1fr)",
              gap: "0.75rem",
              background: "#f9fafb",
              padding: "0.875rem 1rem",
              borderRadius: "0.5rem",
              border: "1px solid #e5e7eb",
              fontSize: "0.8rem",
              marginBottom: "1.25rem",
            }}
          >
            <div>
              <span
                style={{
                  color: "#6b7280",
                  display: "block",
                  fontSize: "0.72rem",
                }}
              >
                Booking Identifier
              </span>
              <strong
                style={{
                  color: "#111827",
                  fontFamily: "monospace",
                  fontSize: "0.82rem",
                }}
              >
                {transaction.bookingId}
              </strong>
            </div>

            <div>
              <span
                style={{
                  color: "#6b7280",
                  display: "block",
                  fontSize: "0.72rem",
                }}
              >
                Parking Facility
              </span>
              <strong style={{ color: "#111827" }}>
                {transaction.lotName}
              </strong>
            </div>

            <div>
              <span
                style={{
                  color: "#6b7280",
                  display: "block",
                  fontSize: "0.72rem",
                }}
              >
                Allocated Parking Slot
              </span>
              <strong style={{ color: "#2563eb" }}>
                {transaction.spaceCode
                  ? `Slot ${transaction.spaceCode}`
                  : "Standard Area"}
              </strong>
            </div>

            <div>
              <span
                style={{
                  color: "#6b7280",
                  display: "block",
                  fontSize: "0.72rem",
                }}
              >
                Payment Method
              </span>
              <strong style={{ color: "#111827" }}>
                {getMethodLabel(transaction.paymentMethod)}
              </strong>
            </div>

            <div>
              <span
                style={{
                  color: "#6b7280",
                  display: "block",
                  fontSize: "0.72rem",
                }}
              >
                Customer / Driver ID
              </span>
              <span style={{ fontFamily: "monospace", color: "#4b5563" }}>
                {transaction.driverId}
              </span>
            </div>

            <div>
              <span
                style={{
                  color: "#6b7280",
                  display: "block",
                  fontSize: "0.72rem",
                }}
              >
                Transaction Status
              </span>
              <strong
                style={{
                  color: isPaid ? "#16a34a" : isFailed ? "#dc2626" : "#d97706",
                  textTransform: "uppercase",
                }}
              >
                {transaction.paymentStatus}
              </strong>
            </div>
          </div>

          {/* Itemized Charges Table (SRS 11) */}
          <div style={{ marginBottom: "1.25rem" }}>
            <table
              style={{
                width: "100%",
                borderCollapse: "collapse",
                fontSize: "0.8rem",
              }}
            >
              <thead>
                <tr
                  style={{
                    borderBottom: "2px solid #e5e7eb",
                    background: "#f3f4f6",
                    textAlign: "left",
                  }}
                >
                  <th
                    style={{
                      padding: "0.5rem 0.6rem",
                      fontWeight: 700,
                      color: "#374151",
                    }}
                  >
                    No.
                  </th>
                  <th
                    style={{
                      padding: "0.5rem 0.6rem",
                      fontWeight: 700,
                      color: "#374151",
                    }}
                  >
                    Description of Service
                  </th>
                  <th
                    style={{
                      padding: "0.5rem 0.6rem",
                      fontWeight: 700,
                      color: "#374151",
                      textAlign: "center",
                    }}
                  >
                    Qty
                  </th>
                  <th
                    style={{
                      padding: "0.5rem 0.6rem",
                      fontWeight: 700,
                      color: "#374151",
                      textAlign: "right",
                    }}
                  >
                    Unit Price (₫)
                  </th>
                  <th
                    style={{
                      padding: "0.5rem 0.6rem",
                      fontWeight: 700,
                      color: "#374151",
                      textAlign: "right",
                    }}
                  >
                    Amount (₫)
                  </th>
                </tr>
              </thead>
              <tbody>
                <tr style={{ borderBottom: "1px solid #f3f4f6" }}>
                  <td style={{ padding: "0.55rem 0.6rem", color: "#6b7280" }}>
                    1
                  </td>
                  <td
                    style={{
                      padding: "0.55rem 0.6rem",
                      color: "#111827",
                      fontWeight: 600,
                    }}
                  >
                    Parking Space Reservation Fee
                    <div
                      style={{
                        fontSize: "0.7rem",
                        color: "#6b7280",
                        fontWeight: 400,
                      }}
                    >
                      Phí trông giữ & đảm bảo vị trí đỗ tại{" "}
                      {transaction.lotName}
                    </div>
                  </td>
                  <td
                    style={{
                      padding: "0.55rem 0.6rem",
                      textAlign: "center",
                      color: "#4b5563",
                    }}
                  >
                    1
                  </td>
                  <td
                    style={{
                      padding: "0.55rem 0.6rem",
                      textAlign: "right",
                      color: "#4b5563",
                    }}
                  >
                    {facilityFee.toLocaleString("vi-VN")}
                  </td>
                  <td
                    style={{
                      padding: "0.55rem 0.6rem",
                      textAlign: "right",
                      fontWeight: 600,
                      color: "#111827",
                    }}
                  >
                    {facilityFee.toLocaleString("vi-VN")}
                  </td>
                </tr>

                <tr style={{ borderBottom: "1px solid #f3f4f6" }}>
                  <td style={{ padding: "0.55rem 0.6rem", color: "#6b7280" }}>
                    2
                  </td>
                  <td
                    style={{
                      padding: "0.55rem 0.6rem",
                      color: "#111827",
                      fontWeight: 600,
                    }}
                  >
                    Platform Access & Automated Gate Control
                    <div
                      style={{
                        fontSize: "0.7rem",
                        color: "#6b7280",
                        fontWeight: 400,
                      }}
                    >
                      Phí điều khiển barrier IoT & bảo vệ đặt chỗ tự động
                    </div>
                  </td>
                  <td
                    style={{
                      padding: "0.55rem 0.6rem",
                      textAlign: "center",
                      color: "#4b5563",
                    }}
                  >
                    1
                  </td>
                  <td
                    style={{
                      padding: "0.55rem 0.6rem",
                      textAlign: "right",
                      color: "#4b5563",
                    }}
                  >
                    {platformFee.toLocaleString("vi-VN")}
                  </td>
                  <td
                    style={{
                      padding: "0.55rem 0.6rem",
                      textAlign: "right",
                      fontWeight: 600,
                      color: "#111827",
                    }}
                  >
                    {platformFee.toLocaleString("vi-VN")}
                  </td>
                </tr>

                <tr style={{ borderBottom: "1px solid #e5e7eb" }}>
                  <td style={{ padding: "0.55rem 0.6rem", color: "#6b7280" }}>
                    3
                  </td>
                  <td style={{ padding: "0.55rem 0.6rem", color: "#111827" }}>
                    Value Added Tax (Thuế GTGT 8%)
                  </td>
                  <td
                    style={{
                      padding: "0.55rem 0.6rem",
                      textAlign: "center",
                      color: "#4b5563",
                    }}
                  >
                    8%
                  </td>
                  <td
                    style={{
                      padding: "0.55rem 0.6rem",
                      textAlign: "right",
                      color: "#4b5563",
                    }}
                  >
                    -
                  </td>
                  <td
                    style={{
                      padding: "0.55rem 0.6rem",
                      textAlign: "right",
                      fontWeight: 600,
                      color: "#111827",
                    }}
                  >
                    {vatAmount.toLocaleString("vi-VN")}
                  </td>
                </tr>
              </tbody>
            </table>
          </div>

          {/* Total Amount Summary */}
          <div
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              padding: "0.85rem 1rem",
              background: "#f8fafc",
              border: "2px solid #e2e8f0",
              borderRadius: "0.5rem",
              marginBottom: "1.25rem",
            }}
          >
            <div>
              <span
                style={{
                  fontSize: "0.75rem",
                  fontWeight: 700,
                  color: "#64748b",
                  textTransform: "uppercase",
                  letterSpacing: "0.05em",
                }}
              >
                TOTAL AMOUNT PAID / TỔNG CỘNG
              </span>
              <div style={{ fontSize: "0.72rem", color: "#94a3b8" }}>
                Inclusive of VAT and platform fees
              </div>
            </div>
            <div
              style={{
                fontSize: "1.4rem",
                fontWeight: 900,
                color: "#0f172a",
                fontFamily: "Outfit, sans-serif",
              }}
            >
              {totalAmount.toLocaleString("vi-VN")} ₫
            </div>
          </div>

          {/* Refund Note if Applicable */}
          {isRefunded && transaction.refundInfo && (
            <div
              style={{
                marginBottom: "1.25rem",
                padding: "0.75rem 1rem",
                background: "#faf5ff",
                border: "1px solid #e9d5ff",
                borderRadius: "0.5rem",
                fontSize: "0.78rem",
                color: "#6b21a8",
              }}
            >
              <strong>Refund Notice:</strong> An amount of{" "}
              <strong>
                {transaction.refundInfo.amount.toLocaleString("vi-VN")} ₫
              </strong>{" "}
              was refunded on{" "}
              {new Date(transaction.refundInfo.refundedAt).toLocaleString(
                "vi-VN",
              )}
              . Reason:{" "}
              {transaction.refundInfo.reason ||
                "Requested by customer/operator"}
              .
            </div>
          )}

          {/* Footer Verification Notice & Electronic Disclaimer */}
          <div
            style={{
              borderTop: "1px dashed #d1d5db",
              paddingTop: "0.85rem",
              display: "flex",
              justifyContent: "space-between",
              alignItems: "flex-end",
              fontSize: "0.72rem",
              color: "#6b7280",
              flexWrap: "wrap",
              gap: "0.75rem",
            }}
          >
            <div style={{ maxWidth: 420 }}>
              <div style={{ fontWeight: 600, color: "#374151" }}>
                Electronic Receipt Notice
              </div>
              <p style={{ margin: "0.15rem 0 0", lineHeight: 1.4 }}>
                This is an official electronic payment document generated by
                SmartPark Vietnam. No physical signature or stamp is required
                pursuant to Decree 123/2020/ND-CP.
              </p>
            </div>

            <div style={{ textAlign: "right" }}>
              <div
                style={{
                  fontFamily: "monospace",
                  fontWeight: 700,
                  color: "#111827",
                  fontSize: "0.75rem",
                }}
              >
                TOKEN: SP-AUTH-{transaction.id.slice(-6).toUpperCase()}
              </div>
              <div
                style={{
                  color: "#9ca3af",
                  fontSize: "0.68rem",
                  marginTop: "0.1rem",
                }}
              >
                System Verification Signature Valid
              </div>
            </div>
          </div>
        </div>

        {/* Modal Bottom Actions (Hidden in Print) */}
        <div
          className="no-print"
          style={{
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            paddingTop: "0.5rem",
          }}
        >
          <button
            type="button"
            className="btn-outline"
            style={{ fontSize: "0.8rem", padding: "0.4rem 0.85rem" }}
            onClick={() => handleCopy(transaction.id)}
          >
            <UntitledIcon name={copied ? "check" : "clipboard"} size={14} />
            <span>{copied ? "Copied ID" : "Copy Transaction ID"}</span>
          </button>

          <div style={{ display: "flex", gap: "0.5rem" }}>
            <button
              type="button"
              className="btn-outline"
              style={{ fontSize: "0.8rem", padding: "0.4rem 0.85rem" }}
              onClick={() => window.print()}
            >
              <UntitledIcon name="file" size={14} /> Print Receipt
            </button>
            <button
              type="button"
              className="btn-primary"
              style={{ fontSize: "0.8rem", padding: "0.4rem 1rem" }}
              onClick={onClose}
            >
              Close
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
