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
  <div class="row spread wrap pagination">
    <span class="muted">
      {{ from }}–{{ to }} / {{ totalCount }} ta
    </span>
    <div class="row">
      <label class="muted size">
        Sahifa hajmi
        <select :value="pageSize" @change="changeSize">
          <option v-for="s in pageSizes" :key="s" :value="s">{{ s }}</option>
        </select>
      </label>
      <button class="secondary" :disabled="page <= 1" @click="go(page - 1)">‹ Oldingi</button>
      <span class="muted">{{ page }} / {{ totalPages }}</span>
      <button class="secondary" :disabled="page >= totalPages" @click="go(page + 1)">Keyingi ›</button>
    </div>
  </div>
</template>

<style scoped>
.pagination {
  margin-top: 14px;
}
.size {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
}
.size select {
  padding: 4px 6px;
}
</style>
