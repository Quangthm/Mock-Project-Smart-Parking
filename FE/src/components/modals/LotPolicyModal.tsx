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
    policyPdf?.title || `Parking Policies & Rules: ${lot.name}`
  const fileName =
    policyPdf?.fileName ||
    `Parking_Policy_${lot.name.replace(/\s+/g, "_")}.pdf`
  const hasUploadedPdf = Boolean(
    policyPdf?.fileData &&
      policyPdf.fileData.startsWith("data:application/pdf"),
  )

  // Fallback printable simulated document data URL if no custom file uploaded
  const downloadUrl = hasUploadedPdf
    ? policyPdf!.fileData
    : `data:text/plain;charset=utf-8,${encodeURIComponent(
        `SMARTPARK - PARKING OPERATIONS POLICY\n\n` +
          `Site: ${lot.name}\n` +
          `Address: ${lot.address}\n` +
          `Type: ${lot.type}\n` +
          `Updated: ${new Date(policyPdf?.uploadedAt || lot.createdAt).toLocaleDateString("en-US")}\n\n` +
          `1. OPERATING HOURS AND RATES:\n` +
          `- Daytime rate: ${lot.hourlyRate.toLocaleString("vi-VN")} VND/hour\n` +
          `- Nighttime rate: ${lot.nightRate.toLocaleString("vi-VN")} VND/hour\n` +
          `- Free grace period: First ${lot.gracePeriodMinutes || 15} minutes after entry\n\n` +
          `2. RESERVATION PROTECTION (SRS v0.9):\n` +
          `- Reservation protection window: 60 minutes from the scheduled arrival time\n` +
          `- Allocation lead time: Up to 15 minutes before arrival\n` +
          `- Reservation deposit: 20% of the estimated total\n\n` +
          `3. CANCELLATIONS AND REFUNDS:\n` +
          `- Cancel at least 30 minutes before arrival: 100% deposit refund to the driver's wallet\n` +
          `- Failure to arrive before the protection window ends: Marked NO_SHOW and the space is released\n\n` +
          `4. SAFETY AND RESPONSIBILITY:\n` +
          `- Follow signs and the 10 km/h speed limit on site\n` +
          `- Do not leave valuables in your vehicle. The site is not responsible for undeclared personal belongings.`,
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
                    fontFamily: "Be Vietnam Pro",
                    fontWeight: 700,
                    fontSize: "1.15rem",
                    margin: 0,
                    color: "var(--fg)",
                  }}
                >
                  Parking Policies & Rules
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
                  {hasUploadedPdf ? "Official PDF" : "Default Document"}
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
            aria-label="Close"
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
            <UntitledIcon name="file" size={14} /> Policy Document (PDF)
          </button>
          <button
            type="button"
            className={activeTab === "summary" ? "btn-primary" : "btn-outline"}
            onClick={() => setActiveTab("summary")}
            style={{ padding: "0.35rem 0.8rem", fontSize: "0.8rem" }}
          >
            <UntitledIcon name="check" size={14} /> Quick Summary
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
                    Attachment:{" "}
                    <strong style={{ color: "var(--fg)" }}>{fileName}</strong> (
                    {policyPdf?.fileSize || "PDF"})
                  </span>
                  <span>
                    Updated:{" "}
                    {new Date(
                      policyPdf?.uploadedAt || Date.now(),
                    ).toLocaleDateString("en-US")}
                  </span>
                </div>
                <iframe
                  src={policyPdf?.fileData}
                  title="Parking policy document"
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
                    SMARTPARK SMART PARKING MANAGEMENT
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
                    Site: <strong>{lot.name}</strong> · Address: {lot.address}
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
                      Section 1. Operating Hours & Service Rates
                    </h5>
                    <p style={{ margin: 0 }}>
                      - Open 24/7 for vehicles registered through SmartParking.
                      <br />- Daytime rate (06:00 - 22:00):{" "}
                      <strong>
                        {lot.hourlyRate.toLocaleString("vi-VN")} VND/hour
                      </strong>
                      .<br />- Nighttime rate (22:00 - 06:00 the next day):{" "}
                      <strong>
                        {lot.nightRate.toLocaleString("vi-VN")} VND/hour
                      </strong>
                      .<br />- Grace period: The first{" "}
                      <strong>{lot.gracePeriodMinutes || 15} minutes</strong> are
                      free for entry processing or turning around.
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
                      Section 2. Reservations & Protection
                      (Reservation Protection Window)
                    </h5>
                    <p style={{ margin: 0 }}>
                      - <strong>Reservation Protection Window</strong>: The space
                      is held for <strong>60 minutes</strong> from the scheduled arrival time.
                      <br />- Deposit: Pay <strong>20%</strong> of the estimated
                      total to confirm the reservation.
                      <br />- Allocation lead time: The system finalizes the
                      assigned space at least 15 minutes before arrival.
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
                      Section 3. Cancellation & No-show Policy
                    </h5>
                    <p style={{ margin: 0 }}>
                      - Cancel at least 30 minutes before arrival: Receive a{" "}
                      <strong>100%</strong> deposit refund.
                      <br />- Cancel within 30 minutes of arrival: A fee equal to
                      one hour of parking is deducted.
                      <br />- No arrival within the 60-minute protection window:
                      The space is released for walk-ins and the deposit is non-refundable.
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
                      Section 4. Safety & Vehicle Responsibility
                    </h5>
                    <p style={{ margin: 0 }}>
                      - Follow signs, lane markings, and the 10 km/h speed limit.
                      <br />- Keep your valuables safe. Contact site management
                      for technical issues, disputes, or surveillance footage.
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
                  Daytime Rate
                </div>
                <div
                  style={{
                    fontSize: "1.1rem",
                    fontWeight: 700,
                    color: "var(--fg)",
                  }}
                >
                  {lot.hourlyRate.toLocaleString("vi-VN")} VND/h
                </div>
                <div
                  style={{
                    fontSize: "0.72rem",
                    color: "var(--muted)",
                    marginTop: "0.2rem",
                  }}
                >
                  Hours: 06:00 – 22:00
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
                  Nighttime Rate
                </div>
                <div
                  style={{
                    fontSize: "1.1rem",
                    fontWeight: 700,
                    color: "var(--fg)",
                  }}
                >
                  {lot.nightRate.toLocaleString("vi-VN")} VND/h
                </div>
                <div
                  style={{
                    fontSize: "0.72rem",
                    color: "var(--muted)",
                    marginTop: "0.2rem",
                  }}
                >
                  Hours: 22:00 – 06:00
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
                  Reservation Protection (SRS)
                </div>
                <div
                  style={{
                    fontSize: "1.1rem",
                    fontWeight: 700,
                    color: "var(--fg)",
                  }}
                >
                  60 Minutes
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
                  Grace Period
                </div>
                <div
                  style={{
                    fontSize: "1.1rem",
                    fontWeight: 700,
                    color: "#22c55e",
                  }}
                >
                  {lot.gracePeriodMinutes || 15} Minutes
                </div>
                <div
                  style={{
                    fontSize: "0.72rem",
                    color: "var(--muted)",
                    marginTop: "0.2rem",
                  }}
                >
                  Free Entry and Exit
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
                  Deposits & Refunds
                </div>
                <div
                  style={{
                    fontSize: "0.85rem",
                    color: "var(--fg)",
                    lineHeight: 1.5,
                  }}
                >
                  ✓ Pay a <strong>20%</strong> deposit to confirm a reservation.
                  <br />✓ Receive a <strong>100%</strong> deposit refund when
                  cancelling at least 30 minutes before arrival.
                  <br />✓ Automatic reallocation is supported if the space
                  requires unexpected maintenance.
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
            <UntitledIcon name="download" size={15} /> Download Policy Document
          </a>

          <button
            type="button"
            className="btn-outline"
            onClick={onClose}
            style={{ fontSize: "0.84rem", padding: "0.45rem 1rem" }}
          >
            Close
          </button>
        </div>
      </div>
    </div>
  )
}
