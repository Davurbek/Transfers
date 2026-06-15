<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { api } from '@/api/client'
import type { PagedResult } from '@/types'

interface AuditLog {
  id: string
  username: string
  actionType: string
  targetTransactionId: string | null
  timestamp: string
  ipAddress: string
  metadata: string | null
}

const items = ref<AuditLog[]>([])
const loading = ref(false)
const error = ref<string | null>(null)

async function load() {
  loading.value = true
  error.value = null
  try {
    const { data } = await api.get<PagedResult<AuditLog>>('/audit')
    items.value = data.items
  } catch {
    error.value = 'Failed to load audit log'
  } finally {
    loading.value = false
  }
}

function fmt(value: string) {
  return new Date(value).toLocaleString()
}

onMounted(load)
</script>

<template>
  <div class="container">
    <h1>Audit log</h1>
    <div class="card">
      <p v-if="error" class="error">{{ error }}</p>
      <p v-else-if="loading" class="muted">Loading…</p>
      <table v-else>
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
    </div>
  </div>
</template>

<style scoped>
h1 {
  font-size: 22px;
  margin: 0 0 16px;
}
</style>
