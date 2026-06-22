<script setup lang="ts">
import { useRouter } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { Permission } from '@/domain/permissions'
import { computed } from 'vue'

const auth = useAuthStore()
const router = useRouter()

const navItems = computed(() => [
  { label: 'Transactions', to: '/', icon: '≡', permission: Permission.TxRead },
  { label: 'Audit log', to: '/audit', icon: '◎', permission: Permission.AuditRead },
].filter(item => !item.permission || auth.hasPermission(item.permission)))

async function handleLogout() {
  await auth.logout()
  router.push({ name: 'login' })
}
</script>

<template>
  <aside class="sidebar">
    <div class="sidebar-brand">
      <div class="brand-icon">T</div>
      <div class="brand-text">
        <strong>Transfers</strong>
        <span class="brand-sub">Ops Dashboard</span>
      </div>
    </div>

    <nav class="sidebar-nav">
      <RouterLink
        v-for="item in navItems"
        :key="item.to"
        :to="item.to"
        class="nav-item"
        :class="{ active: $route.path === item.to }"
      >
        <span class="nav-icon">{{ item.icon }}</span>
        <span class="nav-label">{{ item.label }}</span>
      </RouterLink>
    </nav>

    <div class="sidebar-footer">
      <div class="user-info" v-if="auth.user">
        <div class="user-avatar">{{ auth.user.username.charAt(0).toUpperCase() }}</div>
        <div class="user-details">
          <span class="user-name">{{ auth.user.username }}</span>
          <span class="user-roles">{{ auth.user.roles.join(', ') }}</span>
        </div>
      </div>
      <button class="logout-btn" @click="handleLogout">
        <span>Sign out</span>
      </button>
    </div>
  </aside>
</template>

<style scoped>
.sidebar {
  position: fixed;
  top: 0;
  left: 0;
  width: var(--sidebar-width);
  height: 100vh;
  background: var(--surface);
  border-right: 1px solid var(--border);
  display: flex;
  flex-direction: column;
  z-index: 100;
  overflow-y: auto;
}

.sidebar-brand {
  padding: 24px 20px;
  display: flex;
  align-items: center;
  gap: 12px;
  border-bottom: 1px solid var(--border);
}
.brand-icon {
  width: 38px;
  height: 38px;
  background: linear-gradient(135deg, var(--primary), #8b5cf6);
  border-radius: var(--radius-sm);
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  font-size: 18px;
  color: white;
}
.brand-text {
  display: flex;
  flex-direction: column;
}
.brand-text strong {
  font-size: 15px;
  font-weight: 700;
}
.brand-sub {
  font-size: 11px;
  color: var(--text-dim);
  letter-spacing: 0.03em;
}

.sidebar-nav {
  flex: 1;
  padding: 12px 10px;
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.nav-item {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 10px 12px;
  border-radius: var(--radius-sm);
  color: var(--text-dim);
  font-size: 13px;
  font-weight: 500;
  transition: all var(--transition);
  text-decoration: none;
}
.nav-item:hover {
  background: var(--surface-2);
  color: var(--text);
  text-decoration: none;
}
.nav-item.active {
  background: var(--primary-glow);
  color: var(--primary);
}
.nav-icon {
  width: 24px;
  text-align: center;
  font-size: 16px;
}

.sidebar-footer {
  padding: 16px 14px;
  border-top: 1px solid var(--border);
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.user-info {
  display: flex;
  align-items: center;
  gap: 10px;
}
.user-avatar {
  width: 32px;
  height: 32px;
  border-radius: 50%;
  background: linear-gradient(135deg, var(--primary), #8b5cf6);
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  font-size: 13px;
  color: white;
  flex-shrink: 0;
}
.user-details {
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.user-name {
  font-size: 13px;
  font-weight: 600;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.user-roles {
  font-size: 11px;
  color: var(--text-muted);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.logout-btn {
  background: transparent;
  color: var(--text-dim);
  padding: 7px 12px;
  font-weight: 500;
  justify-content: center;
}
.logout-btn:hover {
  background: var(--danger-bg);
  color: var(--danger);
  box-shadow: none;
  transform: none;
}
</style>
