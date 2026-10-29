interface MfaCodeInputProps {
  recovery: boolean
  value: string
  onChange: (value: string) => void
  onMethodChange: (recovery: boolean) => void
  required?: boolean
  disabled?: boolean
}

export function MfaCodeInput({
  recovery,
  value,
  onChange,
  onMethodChange,
  required = true,
  disabled = false,
}: MfaCodeInputProps) {
  return (
    <div style={{ display: "flex", flexDirection: "column", gap: "0.5rem" }}>
      <label className="label">
        {recovery ? "Recovery code" : "Authenticator code"}
        <input
          className="input"
          type="text"
          required={required}
          disabled={disabled}
          value={value}
          onChange={(event) => onChange(event.target.value)}
          inputMode={recovery ? "text" : "numeric"}
          pattern={recovery ? "[A-Fa-f0-9]{8}(-?[A-Fa-f0-9]{8}){3}" : "[0-9]{6}"}
          maxLength={recovery ? 35 : 6}
          autoComplete="one-time-code"
          placeholder={recovery ? "Enter one of your saved recovery codes" : "Enter the 6-digit code"}
        />
      </label>
      <button
        type="button"
        className="text-button"
        disabled={disabled}
        onClick={() => {
          onMethodChange(!recovery)
          onChange("")
        }}
      >
        {recovery ? "Use authenticator code" : "Use a recovery code"}
      </button>
    </div>
  )
}
