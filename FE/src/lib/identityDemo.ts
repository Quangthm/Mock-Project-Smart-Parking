const completionKey = (userId: string) => `sp_identity_demo_completed:${userId}`

export function hasCompletedIdentityDemo(userId: string): boolean {
  if (!userId) return false
  try {
    return localStorage.getItem(completionKey(userId)) === "1"
  } catch {
    return false
  }
}

export function rememberIdentityDemo(userId: string): boolean {
  if (!userId) return false
  try {
    localStorage.setItem(completionKey(userId), "1")
    return true
  } catch {
    return false
  }
}
