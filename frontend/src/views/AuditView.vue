<script setup lang="ts">
import { onMounted } from 'vue'
import { useRoute } from 'vue-router'
import { useAudit } from '@/composables/useAudit'
import PaginationBar from '@/components/PaginationBar.vue'

const route = useRoute()
const { items, total, loading, error, filters, load, applyFilters, setPage, setPageSize } =
  useAudit({
    targetTransactionId: (route.query.targetTransactionId as string) || undefined,
    actionType: (route.query.actionType as string) || undefined,
    username: (route.query.username as string) || undefined,
    page: route.query.page ? Number(route.query.page) : undefined,
    pageSize: route.query.pageSize ? Number(route.query.pageSize) : undefined,
  })

const actionTypes = ['tx:unpause', 'tx:cancel']

function fmt(value: string) {
  return new Date(value).toLocaleString()
}

onMounted(load)
</script>

<template>
  <div class="container">
    <div class="page-header">
      <div>
        <h1>Audit log</h1>
        <p class="muted" v-if="!loading">{{ total }} total entries</p>
      </div>
      <div class="skeleton" v-if="loading" style="width: 100px; height: 16px" />
    </div>

    <div class="card filter-card">
      <form class="filters" @submit.prevent="applyFilters">
        <input v-model="filters.targetTransactionId" placeholder="Transaction ID…" />
        <select v-model="filters.actionType">
          <option value="">All actions</option>
          <option v-for="a in actionTypes" :key="a" :value="a">{{ a }}</option>
        </select>
        <input v-model="filters.username" placeholder="User…" />
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
          <div class="skeleton" style="width: 18%; height: 14px" />
          <div class="skeleton" style="width: 12%; height: 14px" />
          <div class="skeleton" style="width: 14%; height: 14px" />
          <div class="skeleton" style="width: 18%; height: 14px" />
          <div class="skeleton" style="width: 12%; height: 14px" />
        </div>
      </template>

      <template v-else>
        <div class="table-wrap">
          <table>
            <thead>
              <tr><th>When</th><th>User</th><th>Action</th><th>Transaction</th><th>IP</th></tr>
            </thead>
            <tbody>
              <tr v-for="a in items" :key="a.id">
                <td class="muted">{{ fmt(a.timestamp) }}</td>
                <td>{{ a.username }}</td>
                <td><span class="action-tag">{{ a.actionType }}</span></td>
                <td class="mono">{{ a.targetTransactionId ?? '—' }}</td>
                <td class="muted">{{ a.ipAddress }}</td>
              </tr>
              <tr v-if="!items.length"><td colspan="5" class="empty-state">No audit entries yet.</td></tr>
            </tbody>
          </table>
        </div>

        <PaginationBar
          :page="filters.page ?? 1"
          :page-size="filters.pageSize ?? 50"
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
  font-size: 24px;
  font-weight: 700;
  margin: 0 0 4px;
}
.page-header p {
  font-size: 13px;
  margin: 0;
}
.filter-card {
  margin-bottom: 16px;
  padding: 16px 20px;
}
.filter-card input:first-child {
  flex: 1;
  min-width: 200px;
}
.table-card {
  padding: 0;
  overflow: hidden;
}
.table-wrap {
  overflow-x: auto;
}
.action-tag {
  font-family: ui-monospace, 'SF Mono', Menlo, monospace;
  font-size: 12px;
  background: var(--surface-3);
  padding: 3px 8px;
  border-radius: 4px;
  color: var(--primary);
  font-weight: 500;
}
.empty-state {
  text-align: center;
  color: var(--text-dim);
  padding: 40px 16px !important;
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
