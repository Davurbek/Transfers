import type { PagedQuery } from '@/domain/paging'

const DEFAULT_PAGE = 1

/** Clamps paging and strips empty values so the API receives a clean query. */
export function normalizeQuery<T extends PagedQuery>(query: T, defaultPageSize: number): T {
  const cleaned: Record<string, unknown> = {}

  for (const [key, value] of Object.entries(query)) {
    if (value === '' || value === null || value === undefined) continue
    cleaned[key] = value
  }

  cleaned.page = query.page && query.page > 0 ? query.page : DEFAULT_PAGE
  cleaned.pageSize = query.pageSize && query.pageSize > 0 ? query.pageSize : defaultPageSize

  return cleaned as T
}

/** Converts a datetime-local input value to an ISO string (or undefined). */
export function toIso(value?: string): string | undefined {
  if (!value) return undefined
  const d = new Date(value)
  return Number.isNaN(d.getTime()) ? undefined : d.toISOString()
}
