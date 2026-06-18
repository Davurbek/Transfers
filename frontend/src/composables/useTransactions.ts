import { reactive, ref } from 'vue'
import { transactionService } from '@/di/container'
import type { TransactionSummary } from '@/domain/models'
import type { TransactionQuery } from '@/domain/paging'

/**
 * Presentation logic for the transactions list: reactive state + pagination,
 * delegating data work to the transaction service (BLL).
 */
export function useTransactions() {
  const items = ref<TransactionSummary[]>([])
  const total = ref(0)
  const loading = ref(false)
  const error = ref<string | null>(null)

  const filters = reactive<TransactionQuery>({
    search: '',
    status: '',
    userId: '',
    fromDate: '',
    toDate: '',
    page: 1,
    pageSize: 20,
  })

  async function load(): Promise<void> {
    loading.value = true
    error.value = null
    try {
      const result = await transactionService.search({ ...filters })
      items.value = result.items
      total.value = result.totalCount
    } catch {
      error.value = 'Failed to load transactions'
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
