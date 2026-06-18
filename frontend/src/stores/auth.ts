import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { configureAuthHandlers, setAccessToken } from '@/infrastructure/httpClient'
import { authService } from '@/di/container'
import type { AuthResponse, UserInfo } from '@/domain/models'

/**
 * Presentation-layer session state. Holds the access token + user in memory and
 * delegates all I/O to the auth service (BLL). Also wires the HTTP client's
 * silent-refresh handler to this store.
 */
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
    applyAuth(await authService.login(username, password))
  }

  /** Try to silently re-establish a session using the refresh cookie. */
  async function refresh(): Promise<string | null> {
    try {
      const res = await authService.refresh()
      applyAuth(res)
      return res.accessToken
    } catch {
      clear()
      return null
    }
  }

  async function logout(): Promise<void> {
    try {
      await authService.logout()
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

  // Wire the HTTP client interceptor to this store.
  configureAuthHandlers({ refresh, onUnauthorized: clear })

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
