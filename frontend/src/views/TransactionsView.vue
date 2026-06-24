<script setup lang="ts">
import { onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useTransactions } from '@/composables/useTransactions'
import type { TransactionSummary } from '@/domain/models'
import StatusBadge from '@/components/StatusBadge.vue'
import PaginationBar from '@/components/PaginationBar.vue'

const router = useRouter()
const { items, total, loading, error, filters, load, applyFilters, setPage, setPageSize } =
  useTransactions()

const statuses = [
  'ConfirmPending', 'ConfirmExpired', 'ConfirmFailed', 'ConfirmSucceeded',
  'CreditPending', 'CreditSucceeded', 'CreditFailedRetry', 'CreditFailed',
  'RegistrationPending', 'RegistrationFailedRetry', 'RegistrationSucceeded',
  'Paused', 'Cancelled',
]

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
    <div class="page-header">
      <div>
        <h1>Transactions</h1>
        <p class="muted" v-if="!loading">{{ total }} total transactions</p>
      </div>
      <div class="skeleton" v-if="loading" style="width: 120px; height: 16px" />
    </div>

    <div class="card filter-card">
      <form class="filters" @submit.prevent="applyFilters">
        <input v-model="filters.search" placeholder="Search ID or recipient…" />
        <select v-model="filters.status">
          <option value="">All statuses</option>
          <option v-for="s in statuses" :key="s" :value="s">{{ s }}</option>
        </select>
        <input v-model="filters.userId" placeholder="Sender ID…" />
        <label class="date-label">
          <span>From</span>
          <input v-model="filters.fromDate" type="datetime-local" />
        </label>
        <label class="date-label">
          <span>To</span>
          <input v-model="filters.toDate" type="datetime-local" />
        </label>
        <button type="submit">Filter</button>
      </form>
    </div>

    <div class="card table-card">
      <div v-if="error" class="error-state">
        <p class="error">{{ error }}</p>
        <button class="secondary" @click="load">Retry</button>
      </div>

      <template v-else-if="loading">
        <div class="skeleton-row" v-for="i in 5" :key="i">
          <div class="skeleton" style="width: 20%; height: 14px" />
          <div class="skeleton" style="width: 25%; height: 14px" />
          <div class="skeleton" style="width: 12%; height: 14px" />
          <div class="skeleton" style="width: 15%; height: 14px" />
          <div class="skeleton" style="width: 14%; height: 14px" />
          <div class="skeleton" style="width: 14%; height: 14px" />
        </div>
      </template>

      <template v-else>
        <div class="table-wrap">
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
              <tr v-for="tx in items" :key="tx.transactionId" @click="openDetail(tx)" class="clickable-row">
                <td class="mono">{{ tx.transactionId }}</td>
                <td>{{ tx.recipientName }}</td>
                <td class="amount">{{ tx.amount.toFixed(2) }} {{ tx.currency }}</td>
                <td>{{ tx.corridor }}</td>
                <td><StatusBadge :status="tx.currentStatus" /></td>
                <td class="muted">{{ fmt(tx.createdAt) }}</td>
              </tr>
              <tr v-if="!items.length">
                <td colspan="6" class="empty-state">No transactions found.</td>
              </tr>
            </tbody>
          </table>
        </div>

        <PaginationBar
          :page="filters.page ?? 1"
          :page-size="filters.pageSize ?? 20"
          :total-count="total"
          @update:page="setPage"
          @update:page-size="setPageSize"
        />
      </template>
    </div>
  </div>
</template>

<style scoped>
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-end;
  margin-bottom: 20px;
}
.page-header h1 {
  font-size: 26px;
  font-weight: 800;
  margin: 0 0 4px;
  letter-spacing: -0.03em;
}
.page-header p {
  font-size: 13px;
  margin: 0;
}

.filter-card {
  margin-bottom: 16px;
  padding: 16px 20px;
}
.date-label {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--text-dim);
}
.date-label input {
  width: 180px;
}

.table-card {
  padding: 0;
  overflow: hidden;
}
.table-wrap {
  overflow-x: auto;
}
.clickable-row {
  cursor: pointer;
}
.clickable-row:hover td {
  background: rgba(255, 255, 255, 0.02);
}
.amount {
  font-weight: 700;
  font-variant-numeric: tabular-nums;
  color: var(--success);
}
.empty-state {
  text-align: center;
  color: var(--text-dim);
  padding: 40px 16px !important;
  font-weight: 500;
}
.error-state {
  padding: 40px 24px;
  text-align: center;
}
.error-state .error {
  margin-bottom: 12px;
}
.skeleton-row {
  display: flex;
  gap: 16px;
  padding: 14px 16px;
  border-bottom: 1px solid var(--border);
}
.skeleton-row:last-child {
  border-bottom: none;
}
</style>
