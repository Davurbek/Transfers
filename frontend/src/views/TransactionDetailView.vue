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
    const res = await transactionService.unpause(detail.value.summary.transactionId)
    actionMsg.value = `${res.message} (command ${res.commandId.slice(0, 8)}…)`
    // The new state arrives asynchronously via the consumed event — poll briefly.
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
    if (!detail.value?.summary.isPaused || ticks >= 8) {
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
    <RouterLink to="/" class="muted">&larr; Back to transactions</RouterLink>

    <p v-if="error" class="error">{{ error }}</p>
    <p v-else-if="loading && !detail" class="muted">Loading…</p>

    <template v-else-if="detail">
      <div class="row spread" style="margin: 12px 0 16px">
        <h1 class="mono">{{ detail.summary.transactionId }}</h1>
        <StatusBadge :status="detail.summary.currentStatus" />
      </div>

      <!-- Summary -->
      <div class="card" style="margin-bottom: 16px">
        <div class="grid">
          <div><span class="muted">Recipient</span><div>{{ detail.summary.recipientName }}</div></div>
          <div><span class="muted">Amount</span><div>{{ detail.summary.amount.toFixed(2) }} {{ detail.summary.currency }}</div></div>
          <div><span class="muted">Corridor</span><div>{{ detail.summary.corridor }}</div></div>
          <div><span class="muted">Sender</span><div class="mono">{{ detail.summary.userId }}</div></div>
          <div><span class="muted">Created</span><div>{{ fmt(detail.summary.createdAt) }}</div></div>
          <div><span class="muted">Updated</span><div>{{ fmt(detail.summary.updatedAt) }}</div></div>
        </div>

        <!-- Write action: permission-gated (UI scoping) -->
        <div v-if="detail.summary.isPaused" class="action-bar">
          <PermissionGate :permission="Permission.TxUnpause">
            <button class="danger" :disabled="acting" @click="onUnpause">
              {{ acting ? 'Sending command…' : 'Unpause transaction' }}
            </button>
            <template #denied>
              <span class="muted">This transaction is paused. You lack <code>tx:unpause</code>.</span>
            </template>
          </PermissionGate>
        </div>
        <p v-if="actionMsg" class="muted action-msg">{{ actionMsg }}</p>
      </div>

      <!-- Lifecycle timeline -->
      <div class="card" style="margin-bottom: 16px">
        <h2>Lifecycle history</h2>
        <ol class="timeline">
          <li v-for="(h, i) in detail.statusHistory" :key="i">
            <div class="dot" />
            <div>
              <div class="row" style="gap: 8px">
                <StatusBadge :status="h.toStatus" />
                <span v-if="h.fromStatus" class="muted">from {{ h.fromStatus }}</span>
              </div>
              <div class="muted" v-if="h.reason">{{ h.reason }}</div>
              <div class="muted small">{{ fmt(h.occurredAt) }}</div>
            </div>
          </li>
        </ol>
      </div>

      <!-- Credit attempts -->
      <div class="card" style="margin-bottom: 16px">
        <h2>Credit attempts (Humo / Uzcard)</h2>
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
            <tr v-if="!detail.creditAttempts.length"><td colspan="6" class="muted">No credit attempts.</td></tr>
          </tbody>
        </table>
      </div>

      <!-- Partner registrations -->
      <div class="card">
        <h2>Partner registrations</h2>
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
            <tr v-if="!detail.partnerRegistrations.length"><td colspan="5" class="muted">No partner registrations.</td></tr>
          </tbody>
        </table>
      </div>
    </template>
  </div>
</template>

<style scoped>
h1 {
  font-size: 22px;
  margin: 0;
}
h2 {
  font-size: 15px;
  margin: 0 0 12px;
}
.grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  gap: 16px;
}
.action-bar {
  margin-top: 16px;
  padding-top: 16px;
  border-top: 1px solid var(--border);
}
.action-msg {
  margin-top: 10px;
}
.small {
  font-size: 12px;
}
.timeline {
  list-style: none;
  margin: 0;
  padding: 0;
}
.timeline li {
  position: relative;
  padding: 0 0 18px 22px;
  border-left: 2px solid var(--border);
}
.timeline li:last-child {
  border-left-color: transparent;
}
.dot {
  position: absolute;
  left: -7px;
  top: 4px;
  width: 12px;
  height: 12px;
  border-radius: 50%;
  background: var(--primary);
  border: 2px solid var(--surface);
}
</style>
