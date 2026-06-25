import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '@/stores/auth'
import { Permission } from '@/domain/permissions'

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: () => import('@/views/LoginView.vue'),
    meta: { public: true },
  },
  {
    path: '/',
    name: 'transactions',
    component: () => import('@/views/TransactionsView.vue'),
    meta: { permission: Permission.TxRead },
  },
  {
    path: '/transactions/:id',
    name: 'transaction-detail',
    component: () => import('@/views/TransactionDetailView.vue'),
    props: true,
    meta: { permission: Permission.TxRead },
  },
  {
    path: '/audit',
    name: 'audit',
    component: () => import('@/views/AuditView.vue'),
    meta: { permission: Permission.AuditRead },
  },
  // Admin routes
  {
    path: '/admin/users',
    name: 'users',
    component: () => import('@/views/UsersView.vue'),
    meta: { permission: Permission.Admin },
  },
  {
    path: '/admin/users/:id',
    name: 'user-detail',
    component: () => import('@/views/UserDetailView.vue'),
    props: true,
    meta: { permission: Permission.Admin },
  },
  {
    path: '/admin/roles',
    name: 'roles',
    component: () => import('@/views/RolesView.vue'),
    meta: { permission: Permission.Admin },
  },
  {
    path: '/admin/roles/:id',
    name: 'role-detail',
    component: () => import('@/views/RoleDetailView.vue'),
    props: true,
    meta: { permission: Permission.Admin },
  },
  {
    path: '/admin/permissions',
    name: 'permissions',
    component: () => import('@/views/PermissionsView.vue'),
    meta: { permission: Permission.Admin },
  },
  {
    path: '/forbidden',
    name: 'forbidden',
    component: () => import('@/views/ForbiddenView.vue'),
    meta: { public: true },
  },
  { path: '/:pathMatch(.*)*', redirect: '/' },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

// Global guard: ensure session is restored, enforce auth + permissions.
router.beforeEach(async (to) => {
  const auth = useAuthStore()
  if (!auth.initialized) {
    await auth.bootstrap()
  }

  if (to.meta.public) return true

  if (!auth.isAuthenticated) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }

  const required = to.meta.permission as string | undefined
  if (required && !auth.hasPermission(required)) {
    return { name: 'forbidden' }
  }

  return true
})

export default router
