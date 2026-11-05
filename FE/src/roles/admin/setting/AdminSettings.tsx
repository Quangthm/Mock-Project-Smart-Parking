import { useState } from "react"

import { useApp } from "../../../context/AppContext"

import { adminData as store } from "../data/data"
import { PageHeader } from "../../../components/ui/PageHeader"
import { showUiToast } from "../../../lib/uiToast"
import { authApi } from "../../../lib/authApi"
import { PasswordVisibilityButton } from "../../../components/forms/PasswordVisibilityIcon"

export function AdminSettings() {
  const { user, setUser } = useApp()

  const [form, setForm] = useState({
    name: user?.name ?? "",
    currentPassword: "",
    newPassword: "",
    confirmPassword: "",
  })

  const [error, setError] = useState("")

  const [success, setSuccess] = useState("")
  const [saving, setSaving] = useState(false)
  const [showConfirmPassword, setShowConfirmPassword] = useState(false)

  async function saveProfile(e: React.FormEvent) {
    e.preventDefault()
    if (saving) return

    setError("")
    setSuccess("")

    if (!user) return

    const name = form.name.trim()
    if (!name) {
      setError("Please enter your display name.")
      return
    }

    if (form.newPassword) {
      if (form.currentPassword !== user.password) {
        setError("Current password is incorrect.")
        return
      }

      if (form.newPassword !== form.confirmPassword) {
        setError("New passwords do not match.")
        return
      }

      if (form.newPassword.length < 8) {
        setError("New password must be at least 8 characters.")
        return
      }
    }

    setSaving(true)
    try {
      const confirmedName = name === user.name ? name : (await authApi.updateOwnProfile(name)).fullName
      const updated = { ...user, name: confirmedName, password: form.newPassword || user.password }
      store.saveUser(updated)
      setUser(updated)

      store.addAuditLog({
        userId: user.id,
        userName: user.name,
        userRole: "admin",
        action: "PROFILE_UPDATED",
        details: "Admin profile updated",
      })

      const message = form.newPassword
        ? "Display name saved to your account. Password changes on this page are stored on this device only."
        : "Display name saved to your account."
      setSuccess(message)
      showUiToast("Profile saved", message)
      setForm((f) => ({ ...f, name: confirmedName, currentPassword: "", newPassword: "", confirmPassword: "" }))
      setShowConfirmPassword(false)
    } catch (error) {
      setError(error instanceof Error ? error.message : "Unable to save your profile. Please try again.")
    } finally {
      setSaving(false)
    }
  }

  return (
    <div style={{ maxWidth: "500px" }}>
      <PageHeader title="Admin Settings" variant="compact" />
      <form
        onSubmit={saveProfile}
        className="card"
        style={{ display: "flex", flexDirection: "column", gap: "1rem" }}
      >
        <div>
          <label className="label">Display Name</label>
          <input
            className="input"
            value={form.name}
            onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
          />
        </div>
        <div
          style={{ borderTop: "1px solid var(--border)", paddingTop: "1rem" }}
        >
          <div
            style={{
              fontWeight: 600,
              fontSize: "0.875rem",
              color: "var(--fg)",
              marginBottom: "0.875rem",
            }}
          >
            Change Password
          </div>
          <div
            style={{
              display: "flex",
              flexDirection: "column",
              gap: "0.875rem",
            }}
          >
            <div>
              <label className="label">Current Password</label>
              <input
                className="input"
                type="password"
                value={form.currentPassword}
                onChange={(e) =>
                  setForm((f) => ({ ...f, currentPassword: e.target.value }))
                }
              />
            </div>
            <div>
              <label className="label">New Password</label>
              <input
                className="input"
                type="password"
                value={form.newPassword}
                onChange={(e) =>
                  setForm((f) => ({ ...f, newPassword: e.target.value }))
                }
              />
            </div>
            <div>
              <label className="label">Confirm New Password</label>
              <div style={{ position: "relative" }}>
                <input
                  className="input"
                  type={showConfirmPassword ? "text" : "password"}
                  value={form.confirmPassword}
                  onChange={(e) =>
                    setForm((f) => ({ ...f, confirmPassword: e.target.value }))
                  }
                  style={{ paddingRight: "3.25rem" }}
                />
                <PasswordVisibilityButton visible={showConfirmPassword} onToggle={() => setShowConfirmPassword((value) => !value)} label="new password confirmation" />
              </div>
            </div>
          </div>
        </div>
        {error && (
          <div
            style={{
              background: "#fee2e2",
              border: "1px solid #fca5a5",
              borderRadius: "var(--radius)",
              padding: "0.5rem 0.75rem",
              color: "#dc2626",
              fontSize: "0.85rem",
            }}
          >
            {error}
          </div>
        )}
        {success && (
          <div
            style={{
              background: "#f0fdf4",
              border: "1px solid #86efac",
              borderRadius: "var(--radius)",
              padding: "0.5rem 0.75rem",
              color: "#166534",
              fontSize: "0.85rem",
            }}
          >
            {success}
          </div>
        )}
        <button
          type="submit"
          className="btn-primary"
          style={{ alignSelf: "flex-start" }}
          disabled={saving}
        >
          {saving ? "Saving…" : "Save Changes"}
        </button>
      </form>
    </div>
  )
}
