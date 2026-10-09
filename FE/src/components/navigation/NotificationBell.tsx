import { useEffect, useRef, useState } from "react"

import type { AppNotification } from "../../lib/types"

import { store } from "../../lib/store"

export function NotificationBell({ userId }: { userId: string }) {
  const [open, setOpen] = useState(false)

  const [notifications, setNotifications] = useState<AppNotification[]>(() =>
    store.getNotifications(userId),
  )

  const [toastQueue, setToastQueue] = useState<AppNotification[]>([])

  const rootRef = useRef<HTMLDivElement>(null)

  const knownIdsRef = useRef(new Set(notifications.map((item) => item.id)))

  const unread = notifications.filter((item) => !item.isRead).length

  useEffect(() => {
    const refresh = () => {
      const latest = store.getNotifications(userId)

      const incoming = latest.filter(
        (item) => !knownIdsRef.current.has(item.id),
      )

      latest.forEach((item) => knownIdsRef.current.add(item.id))

      if (incoming.length) setToastQueue((queue) => [...queue, ...incoming])

      setNotifications(latest)
    }

    const closeOnOutside = (event: MouseEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false)
    }

    window.addEventListener("sp-data-change", refresh)

    window.addEventListener("storage", refresh)

    document.addEventListener("mousedown", closeOnOutside)

    return () => {
      window.removeEventListener("sp-data-change", refresh)

      window.removeEventListener("storage", refresh)

      document.removeEventListener("mousedown", closeOnOutside)
    }
  }, [userId])

  useEffect(() => {
    if (!toastQueue.length) return

    const timer = window.setTimeout(
      () => setToastQueue((queue) => queue.slice(1)),
      5200,
    )

    return () => window.clearTimeout(timer)
  }, [toastQueue])

  return (
    <div ref={rootRef} style={{ position: "relative" }}>
      {toastQueue[0] && (
        <div
          key={toastQueue[0].id}
          className="notification-sweep"
          role="status"
          aria-live="polite"
        >
          <strong style={{ display: "block", fontSize: "0.84rem" }}>
            {toastQueue[0].title}
          </strong>
          <span
            style={{
              display: "block",
              marginTop: 3,
              fontSize: "0.78rem",
              lineHeight: 1.4,
            }}
          >
            {toastQueue[0].message}
          </span>
        </div>
      )}
      <button
        type="button"
        onClick={() => setOpen((value) => !value)}
        aria-label={`Notifications${unread ? `, ${unread} unread` : ""}`}
        aria-expanded={open}
        style={{
          position: "relative",
          background: "var(--card)",
          border: "1px solid var(--border)",
          borderRadius: "var(--radius)",
          width: 36,
          height: 36,
          cursor: "pointer",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          color: "var(--fg)",
        }}
      >
        <svg
          width="20"
          height="20"
          viewBox="0 0 24 24"
          fill="none"
          xmlns="http://www.w3.org/2000/svg"
          aria-hidden="true"
        >
          <path
            d="M14 21H10M18 8C18 6.4087 17.3679 4.88258 16.2427 3.75736C15.1174 2.63214 13.5913 2 12 2C10.4087 2 8.8826 2.63214 7.75738 3.75736C6.63216 4.88258 6.00002 6.4087 6.00002 8C6.00002 11.0902 5.22049 13.206 4.34968 14.6054C3.61515 15.7859 3.24788 16.3761 3.26134 16.5408C3.27626 16.7231 3.31488 16.7926 3.46179 16.9016C3.59448 17 4.19261 17 5.38887 17H18.6112C19.8074 17 20.4056 17 20.5382 16.9016C20.6852 16.7926 20.7238 16.7231 20.7387 16.5408C20.7522 16.3761 20.3849 15.7859 19.6504 14.6054C18.7795 13.206 18 11.0902 18 8Z"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
          />
        </svg>
        {unread > 0 && (
          <span
            style={{
              position: "absolute",
              top: -5,
              right: -5,
              minWidth: 17,
              height: 17,
              padding: "0 4px",
              borderRadius: 99,
              background: "#ef4444",
              color: "#fff",
              fontSize: 10,
              lineHeight: "17px",
              fontWeight: 700,
            }}
          >
            {unread > 99 ? "99+" : unread}
          </span>
        )}
      </button>
      {open && (
        <section
          className="notification-pop-in"
          aria-label="Notifications"
          style={{
            position: "absolute",
            right: 0,
            top: "calc(100% + 0.65rem)",
            width: "min(360px, calc(100vw - 2rem))",
            maxHeight: "min(460px, calc(100vh - 80px))",
            overflowY: "auto",
            zIndex: 200,
            background: "var(--bg)",
            border: "1px solid var(--border)",
            borderRadius: "0.875rem",
            boxShadow: "0 16px 40px #0b162822",
            color: "var(--fg)",
          }}
        >
          <header
            style={{
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
              padding: "0.875rem 1rem",
              borderBottom: "1px solid var(--border)",
            }}
          >
            <strong style={{ fontFamily: "Outfit" }}>Notifications</strong>
            <button
              type="button"
              disabled={!unread}
              onClick={() => store.markAllNotificationsRead(userId)}
              style={{
                border: 0,
                background: "none",
                color: unread ? "var(--primary)" : "var(--muted)",
                cursor: unread ? "pointer" : "default",
                fontSize: "0.75rem",
                fontWeight: 600,
              }}
            >
              Mark all as read
            </button>
          </header>
          {notifications.length === 0 ? (
            <p
              style={{
                padding: "1.5rem 1rem",
                margin: 0,
                textAlign: "center",
                color: "var(--muted)",
                fontSize: "0.85rem",
              }}
            >
              You are all caught up.
            </p>
          ) : (
            notifications.map((item) => (
              <button
                type="button"
                key={item.id}
                title={item.isRead ? "Read notification" : "Mark as read"}
                aria-label={`${
                  item.isRead ? "Read" : "Mark as read"
                }: ${item.title}`}
                onClick={() =>
                  !item.isRead && store.markNotificationRead(item.id, userId)
                }
                style={{
                  display: "flex",
                  gap: "0.65rem",
                  width: "100%",
                  padding: "0.85rem 1rem",
                  border: 0,
                  borderBottom: "1px solid var(--border)",
                  background: item.isRead ? "var(--bg)" : "var(--primary)0a",
                  textAlign: "left",
                  cursor: item.isRead ? "default" : "pointer",
                  color: "var(--fg)",
                }}
              >
                <span
                  aria-hidden="true"
                  style={{
                    flexShrink: 0,
                    width: 8,
                    height: 8,
                    marginTop: 5,
                    borderRadius: "50%",
                    background: item.isRead ? "transparent" : "#ef4444",
                  }}
                />
                <span style={{ minWidth: 0 }}>
                  <strong style={{ display: "block", fontSize: "0.82rem" }}>
                    {item.title}
                  </strong>
                  <span
                    style={{
                      display: "block",
                      marginTop: 3,
                      fontSize: "0.78rem",
                      lineHeight: 1.45,
                      color: "var(--muted)",
                    }}
                  >
                    {item.message}
                  </span>
                  <time
                    style={{
                      display: "block",
                      marginTop: 5,
                      fontSize: "0.68rem",
                      color: "var(--muted)",
                    }}
                  >
                    {new Date(item.timestamp).toLocaleString()}
                  </time>
                </span>
              </button>
            ))
          )}
        </section>
      )}
    </div>
  )
}
