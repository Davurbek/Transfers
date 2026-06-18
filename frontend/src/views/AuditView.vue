<script setup lang="ts">
import { onMounted } from 'vue'
import { useAudit } from '@/composables/useAudit'
import PaginationBar from '@/components/PaginationBar.vue'

const { items, total, loading, error, filters, load, applyFilters, setPage, setPageSize } =
  useAudit()

const actionTypes = ['tx:unpause', 'tx:cancel']

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
