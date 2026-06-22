<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import { AxiosError } from 'axios'
import { transactionService } from '@/di/container'
import { Permission } from '@/domain/permissions'
import type { TransactionDetail } from '@/domain/models'
import StatusBadge from '@/components/StatusBadge.vue'
import PermissionGate from '@/components/PermissionGate.vue'

const props = defineProps<{ id: string }>()

const detail = ref<TransactionDetail | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)
const actionMsg = ref<string | null>(null)
const acting = ref(false)
let pollTimer: number | undefined

async function load() {
  loading.value = true
  error.value = null
  try {
    detail.value = await transactionService.getDetail(props.id)
  } catch {
    error.value = 'Failed to load transaction'
  } finally {
    loading.value = false
  }
}

async function onUnpause() {
  if (!detail.value) return
  acting.value = true
  actionMsg.value = null
  try {
    const res = await transactionService.unpause(detail.value.transactionId)
    actionMsg.value = `Command sent (${res.commandId.slice(0, 8)}…)`
    startPolling()
  } catch (e) {
    const ax = e as AxiosError<{ message?: string }>
    actionMsg.value =
      ax.response?.status === 429
        ? 'Rate limit reached for mutations. Try again shortly.'
        : ax.response?.data?.message ?? 'Unpause failed'
  } finally {
    acting.value = false
  }
}

function startPolling() {
  let ticks = 0
  stopPolling()
  pollTimer = window.setInterval(async () => {
    ticks += 1
    await load()
    if (!detail.value?.isPaused || ticks >= 8) {
      stopPolling()
    }
  }, 1500)
}

function stopPolling() {
  if (pollTimer) {
    window.clearInterval(pollTimer)
    pollTimer = undefined
  }
}

function fmt(value: string) {
  return new Date(value).toLocaleString()
}

onMounted(load)
onUnmounted(stopPolling)
</script>

<template>
  <div class="container">
    <RouterLink to="/" class="back-link">&larr; Back to transactions</RouterLink>

    <div v-if="error" class="error-state">
      <p class="error">{{ error }}</p>
      <button class="secondary" @click="load">Retry</button>
    </div>

    <div v-else-if="loading && !detail" class="loading-state">
      <div class="skeleton" style="width: 280px; height: 24px; margin-bottom: 20px" />
      <div class="card"><div class="skeleton" style="width: 100%; height: 120px" /></div>
    </div>

    <template v-else-if="detail">
      <div class="detail-header">
        <div class="detail-title">
          <h1 class="mono">{{ detail.transactionId }}</h1>
          <StatusBadge :status="detail.currentStatus" />
        </div>
        <div class="detail-actions">
          <PermissionGate :permission="Permission.TxUnpause">
            <button
              v-if="detail.isPaused"
              class="danger"
              :disabled="acting"
              @click="onUnpause"
            >
              {{ acting ? 'Unpausing…' : 'Unpause transaction' }}
            </button>
            <template #denied>
              <span v-if="detail.isPaused" class="muted">Paused — you lack unpause permission</span>
            </template>
          </PermissionGate>
        </div>
      </div>
      <p v-if="actionMsg" class="action-msg">{{ actionMsg }}</p>

      <!-- Summary -->
      <div class="card info-card">
        <div class="info-grid">
          <div class="info-item">
            <span class="info-label">Recipient</span>
            <span class="info-value">{{ detail.recipientName }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">Amount</span>
            <span class="info-value amount">{{ detail.amount.toFixed(2) }} {{ detail.currency }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">Corridor</span>
            <span class="info-value">{{ detail.corridor }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">Sender ID</span>
            <span class="info-value mono">{{ detail.userId }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">Created</span>
            <span class="info-value">{{ fmt(detail.createdAt) }}</span>
          </div>
          <div class="info-item">
            <span class="info-label">Updated</span>
            <span class="info-value">{{ fmt(detail.updatedAt) }}</span>
          </div>
        </div>
      </div>

      <!-- Timeline -->
      <div class="card section-card">
        <h2>Lifecycle history</h2>
        <div v-if="!detail.statusHistory.length" class="empty-section">
          No status changes recorded.
        </div>
        <ol class="timeline" v-else>
          <li v-for="(h, i) in detail.statusHistory" :key="i">
            <div class="dot" :class="{ first: i === 0 }" />
            <div class="timeline-content">
              <div class="timeline-header">
                <StatusBadge :status="h.toStatus" />
                <span v-if="h.fromStatus" class="muted">from {{ h.fromStatus }}</span>
              </div>
              <p v-if="h.reason" class="timeline-reason">{{ h.reason }}</p>
              <span class="timeline-time">{{ fmt(h.occurredAt) }}</span>
            </div>
          </li>
        </ol>
      </div>

      <!-- Credit attempts -->
      <div class="card section-card">
        <h2>Credit attempts</h2>
        <div class="table-wrap">
          <table>
            <thead>
              <tr><th>#</th><th>Gateway</th><th>Result</th><th>Code</th><th>Response</th><th>When</th></tr>
            </thead>
            <tbody>
              <tr v-for="c in detail.creditAttempts" :key="c.attemptNumber">
                <td>{{ c.attemptNumber }}</td>
                <td>{{ c.gateway }}</td>
                <td><StatusBadge :status="c.status" /></td>
                <td>{{ c.failureCode ?? '—' }}</td>
                <td class="mono small">{{ c.gatewayResponse ?? '—' }}</td>
                <td class="muted">{{ fmt(c.attemptedAt) }}</td>
              </tr>
              <tr v-if="!detail.creditAttempts.length"><td colspan="6" class="empty-section">No credit attempts.</td></tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- Partner registrations -->
      <div class="card section-card">
        <h2>Partner registrations</h2>
        <div class="table-wrap">
          <table>
            <thead>
              <tr><th>Partner</th><th>Result</th><th>Reference</th><th>Reason</th><th>When</th></tr>
            </thead>
            <tbody>
              <tr v-for="(p, i) in detail.partnerRegistrations" :key="i">
                <td>{{ p.partnerName }}</td>
                <td><StatusBadge :status="p.status" /></td>
                <td class="mono">{{ p.referenceId ?? '—' }}</td>
                <td>{{ p.failureReason ?? '—' }}</td>
                <td class="muted">{{ fmt(p.registeredAt) }}</td>
              </tr>
              <tr v-if="!detail.partnerRegistrations.length"><td colspan="5" class="empty-section">No partner registrations.</td></tr>
            </tbody>
          </table>
        </div>
      </div>
    </template>
  </div>
</template>

<style scoped>
.back-link {
  display: inline-block;
  font-size: 13px;
  margin-bottom: 16px;
}

.detail-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  margin-bottom: 20px;
  flex-wrap: wrap;
  gap: 12px;
}
.detail-title {
  display: flex;
  align-items: center;
  gap: 12px;
}
.detail-title h1 {
  font-size: 22px;
  margin: 0;
}
.detail-actions {
  display: flex;
  gap: 8px;
}
.action-msg {
  margin-bottom: 16px;
  padding: 10px 14px;
  background: var(--success-bg);
  color: var(--success);
  border-radius: var(--radius-sm);
  font-size: 13px;
  font-weight: 500;
}

.info-card {
  margin-bottom: 16px;
}
.info-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 20px;
}
.info-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.info-label {
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  color: var(--text-muted);
  font-weight: 600;
}
.info-value {
  font-size: 14px;
  font-weight: 500;
}
.amount {
  font-weight: 700;
  font-variant-numeric: tabular-nums;
  color: var(--success);
}

.section-card {
  margin-bottom: 16px;
}
.section-card h2 {
  font-size: 14px;
  font-weight: 600;
  margin: 0 0 16px;
  color: var(--text-dim);
  text-transform: uppercase;
  letter-spacing: 0.05em;
}
.table-wrap {
  overflow-x: auto;
}
.small {
  font-size: 12px;
  max-width: 200px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.empty-section {
  text-align: center;
  color: var(--text-dim);
  padding: 32px 16px;
  font-size: 13px;
}
.error-state {
  text-align: center;
  padding: 40px 24px;
}
.error-state .error {
  margin-bottom: 12px;
}
.loading-state {
  padding: 20px 0;
}

.timeline {
  list-style: none;
  margin: 0;
  padding: 0;
}
.timeline li {
  position: relative;
  padding: 0 0 24px 28px;
  border-left: 2px solid var(--border);
}
.timeline li:last-child {
  border-left-color: transparent;
  padding-bottom: 0;
}
.dot {
  position: absolute;
  left: -7px;
  top: 2px;
  width: 12px;
  height: 12px;
  border-radius: 50%;
  background: var(--surface-3);
  border: 2px solid var(--border);
}
.dot.first {
  background: var(--primary);
  border-color: var(--primary);
  box-shadow: 0 0 0 4px var(--primary-glow);
}
.timeline-content {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.timeline-header {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-wrap: wrap;
}
.timeline-reason {
  font-size: 13px;
  color: var(--text);
  margin: 2px 0;
}
.timeline-time {
  font-size: 11px;
  color: var(--text-muted);
}
</style>
