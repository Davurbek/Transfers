import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { api, configureAuthHandlers, setAccessToken } from '@/api/client'
import type { AuthResponse, UserInfo } from '@/types'

export const useAuthStore = defineStore('auth', () => {
  const user = ref<UserInfo | null>(null)
  const accessToken = ref<string | null>(null)
  const initialized = ref(false)

  const isAuthenticated = computed(() => !!user.value && !!accessToken.value)
  const permissions = computed(() => new Set(user.value?.permissions ?? []))

  function hasPermission(permission: string): boolean {
    return permissions.value.has(permission)
  }

  function applyAuth(res: AuthResponse): void {
    user.value = res.user
    accessToken.value = res.accessToken
    setAccessToken(res.accessToken)
  }

  function clear(): void {
    user.value = null
    accessToken.value = null
    setAccessToken(null)
  }

  async function login(username: string, password: string): Promise<void> {
    const { data } = await api.post<AuthResponse>('/auth/login', { username, password })
    applyAuth(data)
  }

  /** Try to silently re-establish a session using the refresh cookie. */
  async function refresh(): Promise<string | null> {
    try {
      const { data } = await api.post<AuthResponse>('/auth/refresh')
      applyAuth(data)
      return data.accessToken
    } catch {
      clear()
      return null
    }
  }

  async function logout(): Promise<void> {
    try {
      await api.post('/auth/logout')
    } finally {
      clear()
    }
  }

  /** Called once on app start to restore a session if a refresh cookie exists. */
  async function bootstrap(): Promise<void> {
    if (initialized.value) return
    await refresh()
    initialized.value = true
  }

  // Wire the axios interceptor to this store.
  configureAuthHandlers({
    refresh,
    onUnauthorized: clear,
  })

  return {
    user,
    accessToken,
    initialized,
    isAuthenticated,
    permissions,
    hasPermission,
    login,
    logout,
    refresh,
    bootstrap,
  }
})
