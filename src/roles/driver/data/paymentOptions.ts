import type { PaymentMethod } from "../../../lib/types"
import visaLogo from "../../../assets/payment-visa.jpg"
import applePayLogo from "../../../assets/payment-applepay.jpg"
import momoLogo from "../../../assets/payment-momo.png"
import vnpayLogo from "../../../assets/payment-vnpay.png"
import zalopayLogo from "../../../assets/payment-zalopay.jpg"
import qrLogo from "../../../assets/payment-qr.png"

export const PAYMENT_OPTIONS: { id: PaymentMethod; name: string; logo: string }[] = [
  { id: "visa", name: "Visa / Mastercard", logo: visaLogo },
  { id: "applepay", name: "Apple Pay", logo: applePayLogo },
  { id: "momo", name: "MoMo", logo: momoLogo },
  { id: "vnpay", name: "VNPAY", logo: vnpayLogo },
  { id: "zalopay", name: "ZaloPay", logo: zalopayLogo },
  { id: "qr", name: "QR Code", logo: qrLogo },
]
