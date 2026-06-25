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
  { label: 'Users', to: '/admin/users', icon: '👤', permission: Permission.Admin },
  { label: 'Roles', to: '/admin/roles', icon: '⚙', permission: Permission.Admin },
  { label: 'Permissions', to: '/admin/permissions', icon: '🔑', permission: Permission.Admin },
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
  background: rgba(12, 18, 34, 0.88);
  backdrop-filter: blur(16px);
  border-right: 1px solid var(--surface-border);
  display: flex;
  flex-direction: column;
  z-index: 100;
  overflow-y: auto;
}

.sidebar-brand {
  padding: 26px 20px 22px;
  display: flex;
  align-items: center;
  gap: 12px;
  border-bottom: 1px solid var(--border);
}
.brand-icon {
  width: 38px;
  height: 38px;
  background: linear-gradient(135deg, #f472b6, var(--purple));
  border-radius: var(--radius-sm);
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  font-size: 18px;
  color: white;
  box-shadow: 0 4px 12px var(--purple-glow);
}
.brand-text {
  display: flex;
  flex-direction: column;
}
.brand-text strong {
  font-size: 16px;
  font-weight: 800;
  letter-spacing: -0.02em;
}
.brand-sub {
  font-size: 10px;
  color: var(--text-dim);
  letter-spacing: 0.06em;
  text-transform: uppercase;
  font-weight: 500;
}

.sidebar-nav {
  flex: 1;
  padding: 14px 10px;
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
  position: relative;
}
.nav-item:hover {
  background: rgba(255, 255, 255, 0.05);
  color: var(--text);
  text-decoration: none;
}
.nav-item.active {
  background: var(--primary-subtle);
  color: var(--primary);
}
.nav-item.active::before {
  content: '';
  position: absolute;
  left: -10px;
  top: 50%;
  transform: translateY(-50%);
  width: 3px;
  height: 20px;
  background: var(--primary);
  border-radius: 0 3px 3px 0;
}
.nav-icon {
  width: 24px;
  text-align: center;
  font-size: 16px;
  opacity: 0.7;
}
.nav-item.active .nav-icon {
  opacity: 1;
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
  padding: 8px 10px;
  background: rgba(255, 255, 255, 0.03);
  border-radius: var(--radius-sm);
  border: 1px solid var(--border);
}
.user-avatar {
  width: 34px;
  height: 34px;
  border-radius: 50%;
  background: linear-gradient(135deg, #22d3ee, var(--primary));
  display: flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  font-size: 14px;
  color: white;
  flex-shrink: 0;
  box-shadow: 0 2px 8px rgba(34, 211, 238, 0.3);
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
  font-size: 10px;
  color: var(--text-muted);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.logout-btn {
  background: transparent;
  color: var(--text-dim);
  padding: 8px 12px;
  font-weight: 500;
  justify-content: center;
  border: 1px solid var(--border);
}
.logout-btn:hover {
  background: var(--danger-bg);
  color: var(--danger);
  border-color: transparent;
  box-shadow: none;
  transform: none;
}
</style>
