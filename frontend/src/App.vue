<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import AppHeader from '@/components/AppHeader.vue'

const auth = useAuthStore()
const route = useRoute()
const showHeader = computed(() => auth.isAuthenticated && !route.meta.public)
</script>

<template>
  <AppHeader v-if="showHeader" />
  <main>
    <RouterView />
  </main>
</template>
