<script setup lang="ts">
import { computed } from 'vue'
import { useAuthStore } from '@/stores/auth'

/**
 * Renders its default slot only if the current user holds the given permission.
 * Used for UI scoping (hide/disable controls per the JWT permission set).
 */
const props = defineProps<{ permission: string }>()
const auth = useAuthStore()
const allowed = computed(() => auth.hasPermission(props.permission))
</script>

<template>
  <slot v-if="allowed" />
  <slot v-else name="denied" />
</template>
