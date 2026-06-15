import axios, {
  AxiosError,
  type AxiosInstance,
  type InternalAxiosRequestConfig,
} from 'axios'

/**
 * Axios instance for the dashboard API.
 *
 * - Attaches the in-memory access token to every request.
 * - On a 401, transparently tries POST /api/auth/refresh (using the HttpOnly
 *   refresh cookie) once, then replays the original request.
 *
 * The access token lives only in memory (never localStorage) to limit XSS
 * exposure; the long-lived refresh token is an HttpOnly cookie the JS can't read.
 */

let accessToken: string | null = null
type RefreshHandler = () => Promise<string | null>
type UnauthorizedHandler = () => void

let onRefresh: RefreshHandler | null = null
let onUnauthorized: UnauthorizedHandler | null = null

export function setAccessToken(token: string | null): void {
  accessToken = token
}

export function configureAuthHandlers(handlers: {
  refresh: RefreshHandler
  onUnauthorized: UnauthorizedHandler
}): void {
  onRefresh = handlers.refresh
  onUnauthorized = handlers.onUnauthorized
}

export const api: AxiosInstance = axios.create({
  baseURL: '/api',
  withCredentials: true, // send the refresh cookie
})

api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  if (accessToken) {
    config.headers.set('Authorization', `Bearer ${accessToken}`)
  }
  return config
})

interface RetriableConfig extends InternalAxiosRequestConfig {
  _retry?: boolean
}

// Single in-flight refresh shared across concurrent 401s.
let refreshPromise: Promise<string | null> | null = null

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as RetriableConfig | undefined
    const status = error.response?.status
    const isAuthCall = original?.url?.includes('/auth/')

    if (status === 401 && original && !original._retry && !isAuthCall && onRefresh) {
      original._retry = true
      try {
        refreshPromise ??= onRefresh()
        const newToken = await refreshPromise
        refreshPromise = null

        if (newToken) {
          original.headers.set('Authorization', `Bearer ${newToken}`)
          return api(original)
        }
      } catch {
        refreshPromise = null
      }
      onUnauthorized?.()
    }

    return Promise.reject(error)
  },
)
