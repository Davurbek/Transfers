<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { fetchAuditLogs, type AuditLog } from '@/api/audit'
import PaginationBar from '@/components/PaginationBar.vue'

const items = ref<AuditLog[]>([])
const total = ref(0)
const loading = ref(false)
const error = ref<string | null>(null)

const filters = reactive({
  targetTransactionId: '',
  actionType: '',
  username: '',
  page: 1,
  pageSize: 50,
})

const actionTypes = ['tx:unpause', 'tx:cancel']

async function load() {
  loading.value = true
  error.value = null
  try {
    const res = await fetchAuditLogs({
      targetTransactionId: filters.targetTransactionId || undefined,
      actionType: filters.actionType || undefined,
      username: filters.username || undefined,
      page: filters.page,
      pageSize: filters.pageSize,
    })
    items.value = res.items
    total.value = res.totalCount
  } catch {
    error.value = 'Failed to load audit log'
  } finally {
    loading.value = false
  }
}

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

function fmt(value: string) {
  return new Date(value).toLocaleString()
}

onMounted(load)
</script>

<template>
  <div class="container">
    <h1>Audit log</h1>

    <div class="card" style="margin-bottom: 16px">
      <form class="filters" @submit.prevent="applyFilters">
        <input v-model="filters.targetTransactionId" placeholder="Transaction id…" />
        <select v-model="filters.actionType">
          <option value="">All actions</option>
          <option v-for="a in actionTypes" :key="a" :value="a">{{ a }}</option>
        </select>
        <input v-model="filters.username" placeholder="User…" />
        <button type="submit">Filter</button>
      </form>
    </div>

    <div class="card">
      <p v-if="error" class="error">{{ error }}</p>
      <p v-else-if="loading" class="muted">Loading…</p>
      <template v-else>
        <table>
          <thead>
            <tr><th>When</th><th>User</th><th>Action</th><th>Transaction</th><th>IP</th></tr>
          </thead>
          <tbody>
            <tr v-for="a in items" :key="a.id">
              <td class="muted">{{ fmt(a.timestamp) }}</td>
              <td>{{ a.username }}</td>
              <td class="mono">{{ a.actionType }}</td>
              <td class="mono">{{ a.targetTransactionId ?? '—' }}</td>
              <td class="muted">{{ a.ipAddress }}</td>
            </tr>
            <tr v-if="!items.length"><td colspan="5" class="muted">No audit entries yet.</td></tr>
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
  margin: 0 0 16px;
}
.filters {
  display: flex;
  gap: 12px;
  flex-wrap: wrap;
  align-items: center;
}
.filters > input:first-child {
  flex: 1;
  min-width: 200px;
}
</style>
