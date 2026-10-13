import { request } from "./authApi"

import type { Role } from "./types"

export interface ManagedUser {
  id: string
  fullName: string
  email?: string
  phone?: string
  roles: Role[]

  status: "active" | "locked" | "pendingVerification" | "pendingApproval" | "rejected"

  createdAt?: string
  lockedUntil?: string
}

export interface UsersPage {
  items: ManagedUser[]
  total: number
  page: number
  pageSize: number
}

export interface UserDetail {
  user: ManagedUser

  loginActivity: Array<{
    createdAt: string
    expiresAt: string
    isRevoked: boolean
  }>
}

interface Response<T> {
  success: boolean
  data: T
}

export const usersApi = {
  async list(query: {
    search: string
    role: string
    status: string
    page: number
  }): Promise<UsersPage> {
    const params = new URLSearchParams({
      page: String(query.page),
      pageSize: "20",
    })

    if (query.search.trim()) params.set("search", query.search.trim())

    if (query.role) params.set("role", query.role)

    if (query.status) params.set("status", query.status)

    const r = await request<Response<UsersPage>>(
      `?${params}`,
      "GET",
      undefined,
      "/api/users",
    )

    if (!r.success || !Array.isArray(r.data?.items))
      throw new Error("Invalid user list response.")

    return r.data
  },

  async get(id: string): Promise<UserDetail> {
    const r = await request<Response<UserDetail>>(
      `/${encodeURIComponent(id)}`,
      "GET",
      undefined,
      "/api/users",
    )

    if (!r.success || !r.data?.user?.id || !Array.isArray(r.data.loginActivity))
      throw new Error("Invalid user details response.")

    return r.data
  },

  async setStatus(
    id: string,
    status: "active" | "locked",
  ): Promise<ManagedUser> {
    const r = await request<Response<ManagedUser>>(
      `/${encodeURIComponent(id)}/status`,
      "PATCH",
      { status },
      "/api/users",
    )

    if (!r.success || !r.data?.id) throw new Error("Invalid status response.")

    return r.data
  },
}
