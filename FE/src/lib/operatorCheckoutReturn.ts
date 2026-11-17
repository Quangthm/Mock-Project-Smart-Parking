export function operatorCheckoutReturnUrls(siteId: string, currentHref = window.location.href) {
  const url = new URL(currentHref)
  url.searchParams.set("checkoutReturn", "payos")
  url.searchParams.set("checkoutSiteId", siteId)
  // PayOS result parameters are navigation hints only; OperationService verifies payment.
  return { returnUrl: url.href, cancelUrl: url.href }
}
