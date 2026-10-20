import { useLayoutEffect, useRef } from "react"

import {
  caretAfterDigits,
  digitsOnly,
  formatGroupedDigits,
} from "../../lib/groupedDigits"

interface GroupedNumberInputProps {
  className: string
  value: string
  onValueChange: (digits: string) => void
}

export function GroupedNumberInput({
  className,
  value,
  onValueChange,
}: GroupedNumberInputProps) {
  const inputRef = useRef<HTMLInputElement>(null)
  const pendingCaret = useRef<number | null>(null)
  const formatted = formatGroupedDigits(value)

  useLayoutEffect(() => {
    if (pendingCaret.current === null) return
    if (document.activeElement === inputRef.current && inputRef.current) {
      inputRef.current.setSelectionRange(pendingCaret.current, pendingCaret.current)
    }
    pendingCaret.current = null
  })

  function handleChange(event: React.ChangeEvent<HTMLInputElement>) {
    const input = event.currentTarget
    const caret = input.selectionStart ?? input.value.length
    const digitsBeforeCaret = digitsOnly(input.value.slice(0, caret)).length
    const nextDigits = digitsOnly(input.value)

    pendingCaret.current = caretAfterDigits(
      formatGroupedDigits(nextDigits),
      digitsBeforeCaret,
    )
    onValueChange(nextDigits)
  }

  function handleKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    const input = event.currentTarget
    const start = input.selectionStart ?? 0
    const end = input.selectionEnd ?? start

    if (start === end && (event.key === "Backspace" || event.key === "Delete")) {
      const separator = event.key === "Backspace" ? start - 1 : start
      if (formatted[separator] === ",") {
        const digitIndex =
          event.key === "Backspace"
            ? digitsOnly(formatted.slice(0, separator)).length - 1
            : digitsOnly(formatted.slice(0, separator)).length
        const nextDigits = value.slice(0, digitIndex) + value.slice(digitIndex + 1)
        pendingCaret.current = caretAfterDigits(
          formatGroupedDigits(nextDigits),
          Math.max(0, digitIndex),
        )
        onValueChange(nextDigits)
        event.preventDefault()
        return
      }
    }

    if (
      event.key.length === 1 &&
      !/^[0-9]$/.test(event.key) &&
      !event.ctrlKey &&
      !event.metaKey &&
      !event.altKey
    ) {
      event.preventDefault()
    }
  }

  return (
    <input
      ref={inputRef}
      className={className}
      type="text"
      inputMode="numeric"
      pattern="[0-9,]*"
      value={formatted}
      onChange={handleChange}
      onKeyDown={handleKeyDown}
    />
  )
}
