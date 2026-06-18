import axios, {
  type AxiosError,
  type AxiosInstance,
  type InternalAxiosRequestConfig,
} from 'axios'

/**
 * Infrastructure layer — the single HTTP client used by the repository layer.
 * Analogous to the backend's DbContext: the only place that talks to the
 * "outside world" (the API). Repositories depend on this; services and views
 * never use axios directly.
 *
 * Responsibilities:
 * - attach the in-memory access token to every request,
 * - transparently refresh once on a 401 and replay the original request.
 *
 * The access token lives only in memory (never localStorage) to limit XSS
 * exposure; the long-lived refresh token is an HttpOnly cookie JS cannot read.
 */

let accessToken: string | null = null
type RefreshHandler = () => Promise<string | null>
type UnauthorizedHandler = () => void

let onRefresh: RefreshHandler | null = null
let onUnauthorized: UnauthorizedHandler | null = null

export function setAccessToken(token: string | null): void {
  accessToken = token
}

/** Wires the auth flow (called by the auth store, which owns the session state). */
export function configureAuthHandlers(handlers: {
  refresh: RefreshHandler
  onUnauthorized: UnauthorizedHandler
}): void {
  onRefresh = handlers.refresh
  onUnauthorized = handlers.onUnauthorized
}

export const http: AxiosInstance = axios.create({
  baseURL: '/api',
  withCredentials: true, // send the refresh cookie
})

http.interceptors.request.use((config: InternalAxiosRequestConfig) => {
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

http.interceptors.response.use(
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
          return http(original)
        }
      } catch {
        refreshPromise = null
      }
      onUnauthorized?.()
    }

    return Promise.reject(error)
  },
)
