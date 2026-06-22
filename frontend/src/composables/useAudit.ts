import { reactive, ref } from 'vue'
import { auditService } from '@/di/container'
import type { AuditLog } from '@/domain/models'
import type { AuditQuery } from '@/domain/paging'

/** Presentation logic for the audit-log view: reactive state + pagination. */
export function useAudit(initial?: Partial<AuditQuery>) {
  const items = ref<AuditLog[]>([])
  const total = ref(0)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const filters = reactive<AuditQuery>({
    targetTransactionId: initial?.targetTransactionId ?? '',
    actionType: initial?.actionType ?? '',
    username: initial?.username ?? '',
    page: initial?.page ?? 1,
    pageSize: initial?.pageSize ?? 50,
  })

  async function load(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const result = await auditService.search({ ...filters })
      items.value = result.items
      total.value = result.totalCount
    } catch {
      error.value = 'Failed to load audit log'
    } finally {
      loading.value = false
    }
  }

  function applyFilters(): void {
    filters.page = 1
    void load()
  }

  function setPage(page: number): void {
    filters.page = page
    void load()
  }

  function setPageSize(pageSize: number): void {
    filters.pageSize = pageSize
    filters.page = 1
    void load()
  }

  return { items, total, loading, error, filters, load, applyFilters, setPage, setPageSize }
}
