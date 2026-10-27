import { useApp, type View } from "../../context/AppContext"

import { BrandLogo, BrandWordmark } from "../brand/BrandLogo"

import { ThemeIcon } from "../icon/ThemeIcon"

export function Navbar() {
  const { user, view, theme, setView, toggleTheme, signOut } = useApp()

  return (
    <nav
      aria-label="Main navigation"
      style={{
        position: "sticky",
        top: 0,
        zIndex: 100,
        background: "var(--sidebar)",
        borderBottom: "1px solid var(--border)",
      }}
    >
      <div
        className="grid grid-cols-[minmax(0,1fr)_auto] items-center gap-x-3 gap-y-2 px-4 py-2 md:grid-cols-[minmax(0,1fr)_auto_minmax(0,1fr)] md:px-6 md:py-0"
        style={{
          maxWidth: "1200px",
          margin: "0 auto",
          minHeight: "60px",
        }}
      >
        {/* Logo */}
        <button
          type="button"
          aria-label="SmartParking home"
          onClick={() => setView("landing")}
          style={{
            display: "flex",
            alignItems: "center",
            gap: "0.5rem",
            background: "none",
            border: "none",
            cursor: "pointer",
            padding: 0,
          }}
        >
          <BrandLogo height={48} />
          <span className="hidden sm:inline-flex"><BrandWordmark height={22} /></span>
        </button>

        {/* Nav links */}
        <div
          className="col-span-2 row-start-2 flex justify-self-center md:col-span-1 md:col-start-2 md:row-start-1"
          style={{
            alignItems: "center",
            gap: "0.25rem",
          }}
        >
          {!user && (
            <>
              <NavLink
                label="Home"
                active={view === "landing"}
                onClick={() => setView("landing")}
              />
              <NavLink
                label="Pricing"
                active={view === "pricing"}
                onClick={() => setView("pricing")}
              />
              <NavLink
                label="Support"
                active={view === "support"}
                onClick={() => setView("support")}
              />
            </>
          )}
          {user && (
            <span style={{ fontSize: "0.85rem", color: "var(--muted)" }}>
              Welcome,{" "}
              <strong style={{ color: "var(--fg)" }}>{user.name}</strong>
            </span>
          )}
        </div>

        {/* Right side */}
        <div
          className="col-start-2 row-start-1 flex justify-self-end md:col-start-3"
          style={{
            alignItems: "center",
            gap: "0.5rem",
          }}
        >
          <button
            type="button"
            className="public-theme-toggle"
            onClick={toggleTheme}
            style={{
              background: "var(--card)",
              border: "1px solid var(--border)",
              borderRadius: "var(--button-radius)",
              width: 44,
              height: 44,
              cursor: "pointer",
              display: "flex",
              alignItems: "center",
              justifyContent: "center",
              color: "var(--fg)",
            }}
            title={
              theme === "dark" ? "Switch to Light Mode" : "Switch to Dark Mode"
            }
            aria-label={theme === "dark" ? "Switch to Light Mode" : "Switch to Dark Mode"}
          >
            <span style={{ display: "block", width: 20, height: 20 }}>
              <ThemeIcon mode={theme === "dark" ? "light" : "dark"} />
            </span>
          </button>

          {user ? (
            <button
              className="btn-outline"
              style={{ fontSize: "0.85rem", padding: "0.4rem 0.875rem" }}
              onClick={signOut}
            >
              Sign Out
            </button>
          ) : (
            <>
              <button
                className="btn-outline"
                style={{ fontSize: "0.85rem", padding: "0.4rem 0.875rem" }}
                onClick={() => setView("sign-in")}
              >
                Sign In
              </button>
              <button
                className="btn-primary"
                style={{ fontSize: "0.85rem", padding: "0.4rem 0.875rem" }}
                onClick={() => setView("sign-up")}
              >
                Sign Up
              </button>
            </>
          )}
        </div>
      </div>
    </nav>
  )
}

function NavLink({
  label,
  active,
  onClick,
}: {
  label: string
  active: boolean
  onClick: () => void
}) {
  return (
    <button
      type="button"
      aria-current={active ? "page" : undefined}
      onClick={onClick}
      style={{
        background: "none",
        border: "none",
        cursor: "pointer",

        padding: "0.625rem 0.75rem",

        borderRadius: "var(--radius)",

        fontSize: "0.9rem",

        fontWeight: active ? 600 : 400,

        color: active ? "var(--primary)" : "var(--muted)",

        transition: "color 0.15s",
      }}
    >
      {label}
    </button>
  )
}
