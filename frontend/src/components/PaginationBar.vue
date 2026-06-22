<script setup lang="ts">
import { computed } from 'vue'

const props = defineProps<{
  page: number
  pageSize: number
  totalCount: number
}>()

const emit = defineEmits<{
  (e: 'update:page', value: number): void
  (e: 'update:pageSize', value: number): void
}>()

const totalPages = computed(() =>
  props.pageSize > 0 ? Math.max(1, Math.ceil(props.totalCount / props.pageSize)) : 1,
)
const from = computed(() => (props.totalCount === 0 ? 0 : (props.page - 1) * props.pageSize + 1))
const to = computed(() => Math.min(props.page * props.pageSize, props.totalCount))

const pageSizes = [10, 20, 50, 100]

function go(p: number) {
  if (p >= 1 && p <= totalPages.value && p !== props.page) emit('update:page', p)
}
function changeSize(e: Event) {
  emit('update:pageSize', Number((e.target as HTMLSelectElement).value))
}
</script>

<template>
  <div class="pagination">
    <span class="pagination-info">{{ from }}–{{ to }} / {{ totalCount }}</span>
    <div class="pagination-controls">
      <label class="page-size">
        <span>Per page</span>
        <select :value="pageSize" @change="changeSize">
          <option v-for="s in pageSizes" :key="s" :value="s">{{ s }}</option>
        </select>
      </label>
      <div class="page-nav">
        <button class="secondary page-btn" :disabled="page <= 1" @click="go(page - 1)">‹</button>
        <span class="page-indicator">{{ page }} / {{ totalPages }}</span>
        <button class="secondary page-btn" :disabled="page >= totalPages" @click="go(page + 1)">›</button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.pagination {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 14px 20px;
  border-top: 1px solid var(--border);
  flex-wrap: wrap;
  gap: 12px;
}
.pagination-info {
  font-size: 12px;
  color: var(--text-dim);
  font-variant-numeric: tabular-nums;
}
.pagination-controls {
  display: flex;
  align-items: center;
  gap: 16px;
}
.page-size {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--text-dim);
}
.page-size select {
  padding: 4px 6px;
  font-size: 12px;
}
.page-nav {
  display: flex;
  align-items: center;
  gap: 8px;
}
.page-btn {
  padding: 6px 12px;
  font-size: 16px;
  line-height: 1;
}
.page-indicator {
  font-size: 12px;
  color: var(--text-dim);
  font-variant-numeric: tabular-nums;
  min-width: 60px;
  text-align: center;
}
</style>
