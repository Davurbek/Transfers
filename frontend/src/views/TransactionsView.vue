<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { useRouter } from 'vue-router'
import { fetchTransactions } from '@/api/transactions'
import type { TransactionSummary } from '@/types'
import StatusBadge from '@/components/StatusBadge.vue'
import PaginationBar from '@/components/PaginationBar.vue'

const router = useRouter()
const items = ref<TransactionSummary[]>([])
const total = ref(0)
const loading = ref(false)
const error = ref<string | null>(null)

const filters = reactive({
  search: '',
  status: '',
  userId: '',
  fromDate: '',
  toDate: '',
  page: 1,
  pageSize: 20,
})

const statuses = [
  'ConfirmPending', 'ConfirmSucceeded', 'CreditPending', 'CreditSucceeded',
  'RegistrationPending', 'RegistrationFailedRetry', 'RegistrationSucceeded',
  'Paused', 'Cancelled',
]

async function load() {
  loading.value = true
  error.value = null
  try {
    const res = await fetchTransactions({
      search: filters.search || undefined,
      status: filters.status || undefined,
      userId: filters.userId || undefined,
      fromDate: filters.fromDate ? new Date(filters.fromDate).toISOString() : undefined,
      toDate: filters.toDate ? new Date(filters.toDate).toISOString() : undefined,
      page: filters.page,
      pageSize: filters.pageSize,
    })
    items.value = res.items
    total.value = res.totalCount
  } catch {
    error.value = 'Failed to load transactions'
  } finally {
    loading.value = false
  }
}

// Searching/filtering resets to the first page.
function applyFilters() {
  filters.page = 1
  load()
}

function setPage(p: number) {
  filters.page = p
  load()
}

function setPageSize(s: number) {
  filters.pageSize = s
  filters.page = 1
  load()
}

function openDetail(tx: TransactionSummary) {
  router.push({ name: 'transaction-detail', params: { id: tx.transactionId } })
}

function fmt(value: string) {
  return new Date(value).toLocaleString()
}

onMounted(load)
</script>

<template>
  <div class="container">
    <div class="row spread" style="margin-bottom: 16px">
      <h1>Transactions</h1>
      <span class="muted">{{ total }} total</span>
    </div>

    <div class="card" style="margin-bottom: 16px">
      <form class="filters" @submit.prevent="applyFilters">
        <input v-model="filters.search" placeholder="Search id or recipient…" />
        <select v-model="filters.status">
          <option value="">All statuses</option>
          <option v-for="s in statuses" :key="s" :value="s">{{ s }}</option>
        </select>
        <input v-model="filters.userId" placeholder="Sender id…" />
        <label class="date">From <input v-model="filters.fromDate" type="datetime-local" /></label>
        <label class="date">To <input v-model="filters.toDate" type="datetime-local" /></label>
        <button type="submit">Filter</button>
      </form>
    </div>

    <div class="card">
      <p v-if="error" class="error">{{ error }}</p>
      <p v-else-if="loading" class="muted">Loading…</p>
      <template v-else>
        <table>
          <thead>
            <tr>
              <th>Transaction</th>
              <th>Recipient</th>
              <th>Amount</th>
              <th>Corridor</th>
              <th>Status</th>
              <th>Created</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="tx in items" :key="tx.transactionId" @click="openDetail(tx)" style="cursor: pointer">
              <td class="mono">{{ tx.transactionId }}</td>
              <td>{{ tx.recipientName }}</td>
              <td>{{ tx.amount.toFixed(2) }} {{ tx.currency }}</td>
              <td>{{ tx.corridor }}</td>
              <td><StatusBadge :status="tx.currentStatus" /></td>
              <td class="muted">{{ fmt(tx.createdAt) }}</td>
            </tr>
            <tr v-if="!items.length">
              <td colspan="6" class="muted">No transactions found.</td>
            </tr>
          </tbody>
        </table>

        <PaginationBar
          :page="filters.page"
          :page-size="filters.pageSize"
          :total-count="total"
          @update:page="setPage"
          @update:page-size="setPageSize"
        />
      </template>
    </div>
  </div>
</template>

<style scoped>
h1 {
  font-size: 22px;
  margin: 0;
}
.filters {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
  align-items: center;
}
.filters input,
.filters select {
  flex: 0 0 auto;
}
.filters > input:first-child {
  flex: 1;
  min-width: 200px;
}
.date {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--text-dim);
}
</style>
