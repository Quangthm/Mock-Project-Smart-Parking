import type { PaymentMethod } from "../../../lib/types"

import visaLogo from "../../../assets/payment-visa.webp"

import applePayLogo from "../../../assets/Apple Pay.png"

import momoLogo from "../../../assets/Momo.jpg"

import vnpayLogo from "../../../assets/payment-vnpay.png"

import zalopayLogo from "../../../assets/payment-zalopay.jpg"

import qrLogo from "../../../assets/payment-qr.png"

export { visaLogo, applePayLogo, momoLogo, vnpayLogo, zalopayLogo, qrLogo }

export const PAYMENT_LOGOS: Record<string, string> = {
  visa: visaLogo,

  applepay: applePayLogo,

  momo: momoLogo,

  vnpay: vnpayLogo,

  zalopay: zalopayLogo,

  qr: qrLogo,
}

export const PAYMENT_OPTIONS: {
  id: PaymentMethod
  name: string
  logo: string
}[] = [
  { id: "visa", name: "Visa / Mastercard", logo: visaLogo },

  { id: "applepay", name: "Apple Pay", logo: applePayLogo },

  { id: "momo", name: "MoMo", logo: momoLogo },

  { id: "vnpay", name: "VNPAY", logo: vnpayLogo },

  { id: "zalopay", name: "ZaloPay", logo: zalopayLogo },

  { id: "qr", name: "QR Code (VietQR)", logo: qrLogo },
]

export function getPaymentMethodInfo(method: string) {
  const normalized = method?.toLowerCase() || ""

  const option = PAYMENT_OPTIONS.find((opt) => opt.id === normalized)

  if (option) return option

  if (normalized.includes("momo"))
    return { id: "momo" as PaymentMethod, name: "MoMo", logo: momoLogo }

  if (normalized.includes("apple"))
    return {
      id: "applepay" as PaymentMethod,
      name: "Apple Pay",
      logo: applePayLogo,
    }

  if (normalized.includes("visa") || normalized.includes("card"))
    return {
      id: "visa" as PaymentMethod,
      name: "Visa / Mastercard",
      logo: visaLogo,
    }

  if (normalized.includes("vn") || normalized.includes("vnpay"))
    return { id: "vnpay" as PaymentMethod, name: "VNPAY", logo: vnpayLogo }

  if (normalized.includes("zalo"))
    return {
      id: "zalopay" as PaymentMethod,
      name: "ZaloPay",
      logo: zalopayLogo,
    }

  if (normalized.includes("qr"))
    return { id: "qr" as PaymentMethod, name: "QR Code (VietQR)", logo: qrLogo }

  return {
    id: normalized as PaymentMethod,
    name: method.toUpperCase(),
    logo: qrLogo,
  }
}
