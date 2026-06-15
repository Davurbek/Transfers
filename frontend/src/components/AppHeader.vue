<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { Permission } from '@/types'

const auth = useAuthStore()
const router = useRouter()

async function handleLogout() {
  await auth.logout()
  router.push({ name: 'login' })
}
</script>

<template>
  <header class="app-header">
    <div class="container row spread">
      <nav class="row">
        <strong class="brand">Transfers&nbsp;·&nbsp;Ops</strong>
        <RouterLink to="/" v-if="auth.hasPermission(Permission.TxRead)">Transactions</RouterLink>
        <RouterLink to="/audit" v-if="auth.hasPermission(Permission.AuditRead)">Audit log</RouterLink>
      </nav>
      <div class="row" v-if="auth.user">
        <span class="muted">
          {{ auth.user.username }}
          <span class="roles">({{ auth.user.roles.join(', ') }})</span>
        </span>
        <button class="secondary" @click="handleLogout">Sign out</button>
      </div>
    </div>
  </header>
</template>

<style scoped>
.app-header {
  background: var(--surface);
  border-bottom: 1px solid var(--border);
}
.app-header .container {
  padding: 12px 20px;
}
.brand {
  margin-right: 8px;
}
nav a {
  padding: 4px 8px;
  color: var(--text-dim);
}
nav a.router-link-active {
  color: var(--text);
}
.roles {
  font-size: 12px;
}
</style>
