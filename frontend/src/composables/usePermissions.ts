import { useAuthStore } from '@/stores/auth'

/** Presentation helper for permission-aware rendering (UI scoping). */
export function usePermissions() {
  const auth = useAuthStore()
  const can = (permission: string) => auth.hasPermission(permission)
  return { can }
}
