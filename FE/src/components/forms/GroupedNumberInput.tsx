import { useLayoutEffect, useRef } from "react"

import {
  caretAfterDigits,
  digitsOnly,
  formatGroupedDigits,
} from "../../lib/groupedDigits"

interface GroupedNumberInputProps {
  className: string
  value: string
  placeholder?: string
  onValueChange: (digits: string) => void
}

export function GroupedNumberInput({
  className,
  value,
  placeholder,
  onValueChange,
}: GroupedNumberInputProps) {
  const inputRef = useRef<HTMLInputElement>(null)
  const pendingCaret = useRef<number | null>(null)
  const isSelectedRef = useRef(false)
  const formatted = formatGroupedDigits(value)

  useLayoutEffect(() => {
    if (pendingCaret.current === null) return
    if (document.activeElement === inputRef.current) {
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

    // If typing stripped characters (like commas or non-digits) so nextDigits equals current value,
    // React won't re-render, leaving stripped characters in DOM input. Force sync DOM value:
    if (nextDigits === value && inputRef.current) {
      inputRef.current.value = formatGroupedDigits(nextDigits)
      if (pendingCaret.current !== null) {
        inputRef.current.setSelectionRange(pendingCaret.current, pendingCaret.current)
        pendingCaret.current = null
      }
    }

    onValueChange(nextDigits)
  }

  function handleKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    const input = event.currentTarget
    const start = input.selectionStart ?? 0
    const end = input.selectionEnd ?? start

    // If user presses Backspace right after a comma (e.g. "15,|000"):
    // Delete the digit before the comma cleanly without getting stuck on comma
    if (start === end && event.key === "Backspace") {
      if (start > 0 && formatted[start - 1] === ",") {
        const digitIndex = digitsOnly(formatted.slice(0, start - 1)).length - 1
        if (digitIndex >= 0) {
          const nextDigits = value.slice(0, digitIndex) + value.slice(digitIndex + 1)
          pendingCaret.current = caretAfterDigits(
            formatGroupedDigits(nextDigits),
            digitIndex,
          )
          onValueChange(nextDigits)
          event.preventDefault()
          return
        }
      }
    }

    // If user presses Delete right before a comma (e.g. "15|,000"):
    // Delete the digit after the comma
    if (start === end && event.key === "Delete") {
      if (start < formatted.length && formatted[start] === ",") {
        const digitIndex = digitsOnly(formatted.slice(0, start)).length
        if (digitIndex < value.length) {
          const nextDigits = value.slice(0, digitIndex) + value.slice(digitIndex + 1)
          pendingCaret.current = caretAfterDigits(
            formatGroupedDigits(nextDigits),
            digitIndex,
          )
          onValueChange(nextDigits)
          event.preventDefault()
          return
        }
      }
    }

    // Ignore explicit comma or period keypresses since commas are auto-formatted
    if (event.key === "," || event.key === ".") {
      event.preventDefault()
      return
    }

    // Block non-numeric characters (allow Ctrl/Meta combinations for copy/paste/select-all)
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

  function handleFocus(event: React.FocusEvent<HTMLInputElement>) {
    isSelectedRef.current = false
    const target = event.currentTarget
    requestAnimationFrame(() => target.select())
  }

  function handleMouseUp(event: React.MouseEvent<HTMLInputElement>) {
    const target = event.currentTarget
    if (!isSelectedRef.current && target.value) {
      target.select()
      isSelectedRef.current = true
      event.preventDefault()
    }
  }

  function handleBlur() {
    isSelectedRef.current = false
  }

  return (
    <input
      ref={inputRef}
      className={className}
      type="text"
      inputMode="numeric"
      pattern="[0-9,]*"
      placeholder={placeholder}
      value={formatted}
      onFocus={handleFocus}
      onMouseUp={handleMouseUp}
      onBlur={handleBlur}
      onChange={handleChange}
      onKeyDown={handleKeyDown}
    />
  )
}
