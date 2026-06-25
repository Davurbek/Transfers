<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{ status: string }>()

const tone = computed(() => {
  const s = props.status
  if (s.endsWith('Succeeded')) return 'success'
  if (s.endsWith('Failed')) return 'danger'
  if (s === 'Paused' || s.endsWith('FailedRetry')) return 'warning'
  return 'neutral'
})
</script>

<template>
  <span class="badge" :class="tone">{{ status }}</span>
</template>

<style scoped>
.badge {
  display: inline-block;
  padding: 3px 11px;
  border-radius: 999px;
  font-size: 11px;
  font-weight: 700;
  border: 1px solid transparent;
  letter-spacing: 0.01em;
}
.success {
  background: var(--success-bg);
  color: var(--success);
  border-color: rgba(52, 211, 153, 0.35);
  box-shadow: 0 0 8px rgba(52, 211, 153, 0.1);
}
.danger {
  background: var(--danger-bg);
  color: var(--danger);
  border-color: rgba(248, 113, 113, 0.35);
  box-shadow: 0 0 8px rgba(248, 113, 113, 0.1);
}
.warning {
  background: var(--warning-bg);
  color: var(--warning);
  border-color: rgba(251, 191, 36, 0.35);
  box-shadow: 0 0 8px rgba(251, 191, 36, 0.1);
}
.neutral {
  background: rgba(148, 163, 184, 0.1);
  color: #b0c8e8;
  border-color: rgba(148, 163, 184, 0.25);
}
</style>
