<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { fetchTransactions } from '@/api/transactions'
import type { TransactionSummary } from '@/types'
import StatusBadge from '@/components/StatusBadge.vue'

const router = useRouter()
const items = ref<TransactionSummary[]>([])
const total = ref(0)
const loading = ref(false)
const error = ref<string | null>(null)

const search = ref('')
const status = ref('')

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
      search: search.value || undefined,
      status: status.value || undefined,
    })
    items.value = res.items
    total.value = res.totalCount
  } catch {
    error.value = 'Failed to load transactions'
  } finally {
    loading.value = false
  }
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
      <form class="row wrap" @submit.prevent="load">
        <input v-model="search" placeholder="Search id or recipient…" style="flex: 1; min-width: 220px" />
        <select v-model="status">
          <option value="">All statuses</option>
          <option v-for="s in statuses" :key="s" :value="s">{{ s }}</option>
        </select>
        <button type="submit">Search</button>
      </form>
    </div>

    <div class="card">
      <p v-if="error" class="error">{{ error }}</p>
      <p v-else-if="loading" class="muted">Loading…</p>
      <table v-else>
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
    </div>
  </div>
</template>

<style scoped>
h1 {
  font-size: 22px;
  margin: 0;
}
</style>
