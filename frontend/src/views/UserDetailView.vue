<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { adminService } from '@/di/container'
import type { UserDetail, RoleListItem, PermissionInfo } from '@/domain/models'

const route = useRoute()
const router = useRouter()
const userId = route.params.id as string

const user = ref<UserDetail | null>(null)
const allRoles = ref<RoleListItem[]>([])
const allPermissions = ref<PermissionInfo[]>([])
const loading = ref(true)
const error = ref('')
const editing = ref(false)
const editForm = ref({ username: '', email: '', isActive: true })
const saving = ref(false)
const selectedRoleId = ref('')
const selectedPermId = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [u, roles, perms] = await Promise.all([
      adminService.getUser(userId),
      adminService.getAllRoles(),
      adminService.getAllPermissions(),
    ])
    user.value = u
    allRoles.value = roles
    allPermissions.value = perms
    editForm.value = { username: u.username, email: u.email, isActive: u.isActive }
  } catch (e: any) {
    error.value = e?.response?.data?.message || e.message || 'User not found'
  } finally {
    loading.value = false
  }
}

async function saveUser() {
  saving.value = true
  try {
    user.value = await adminService.updateUser(userId, editForm.value)
    editing.value = false
  } catch (e: any) {
    error.value = e?.response?.data?.message || e.message || 'Failed to update'
  } finally {
    saving.value = false
  }
}

async function addRole() {
  if (!selectedRoleId.value) return
  try {
    await adminService.addUserRole(userId, selectedRoleId.value)
    selectedRoleId.value = ''
    await load()
  } catch (e: any) {
    error.value = e?.response?.data?.message || e.message || 'Failed to add role'
  }
}

async function removeRole(roleId: string) {
  try {
    await adminService.removeUserRole(userId, roleId)
    await load()
  } catch (e: any) {
    error.value = e?.response?.data?.message || e.message || 'Failed to remove role'
  }
}

async function addDirectPerm() {
  if (!selectedPermId.value) return
  try {
    await adminService.addUserPermission(userId, selectedPermId.value)
    selectedPermId.value = ''
    await load()
  } catch (e: any) {
    error.value = e?.response?.data?.message || e.message || 'Failed to add permission'
  }
}

async function removeDirectPerm(permId: string) {
  try {
    await adminService.removeUserPermission(userId, permId)
    await load()
  } catch (e: any) {
    error.value = e?.response?.data?.message || e.message || 'Failed to remove permission'
  }
}

async function deleteUser() {
  if (!confirm('Deactivate this user?')) return
  try {
    await adminService.deleteUser(userId)
    router.push({ name: 'users' })
  } catch (e: any) {
    error.value = e?.response?.data?.message || e.message || 'Failed to deactivate'
  }
}

const availableRoles = computed(() =>
  allRoles.value.filter(r => !user.value?.roles.some(ur => ur.id === r.id))
)

const availablePerms = computed(() =>
  allPermissions.value.filter(p => !user.value?.permissions.some(up => up.id === p.id))
)

onMounted(load)
</script>

<template>
  <div class="container" v-if="!loading && user">
    <div class="page-header">
      <div>
        <a class="back-link" @click="router.push({ name: 'users' })">← Users</a>
        <h1>{{ user.username }}</h1>
      </div>
      <div class="row">
        <button class="secondary" @click="editing = !editing">{{ editing ? 'Cancel' : 'Edit' }}</button>
        <button class="danger" @click="deleteUser">Deactivate</button>
      </div>
    </div>

    <div class="card" v-if="editing">
      <h3>Edit User</h3>
      <form class="edit-form" @submit.prevent="saveUser">
        <input v-model="editForm.username" placeholder="Username" required />
        <input v-model="editForm.email" type="email" placeholder="Email" required />
        <label><input v-model="editForm.isActive" type="checkbox" /> Active</label>
        <button type="submit" :disabled="saving">{{ saving ? 'Saving…' : 'Save' }}</button>
      </form>
    </div>

    <div class="card">
      <div class="detail-row"><span class="label">Email</span><span>{{ user.email }}</span></div>
      <div class="detail-row"><span class="label">Active</span><span :class="user.isActive ? 'status-ok' : 'status-muted'">{{ user.isActive ? 'Yes' : 'No' }}</span></div>
      <div class="detail-row"><span class="label">Created</span><span class="muted">{{ new Date(user.createdAt).toLocaleString() }}</span></div>
    </div>

    <div class="card">
      <h3>Roles</h3>
      <div class="assign-row">
        <select v-model="selectedRoleId">
          <option value="">Add role…</option>
          <option v-for="r in availableRoles" :key="r.id" :value="r.id">{{ r.name }}</option>
        </select>
        <button :disabled="!selectedRoleId" @click="addRole">Add</button>
      </div>
      <div v-if="(user.roles ?? []).length">
        <div v-for="r in (user.roles ?? [])" :key="r.id" class="assigned-item">
          <span>{{ r.name }}</span>
          <button class="secondary small" @click="removeRole(r.id)">✕</button>
        </div>
      </div>
      <p v-else class="muted">No roles assigned.</p>
    </div>

    <div class="card">
      <h3>Direct Permissions</h3>
      <p class="muted" style="font-size:12px;margin-bottom:10px">Permissions assigned directly to this user (not inherited from roles).</p>
      <div class="assign-row">
        <select v-model="selectedPermId">
          <option value="">Add permission…</option>
          <option v-for="p in availablePerms" :key="p.id" :value="p.id">{{ p.code }}</option>
        </select>
        <button :disabled="!selectedPermId" @click="addDirectPerm">Add</button>
      </div>
      <div v-if="(user.permissions ?? []).length">
        <div v-for="p in (user.permissions ?? [])" :key="p.id" class="assigned-item">
          <div><strong>{{ p.code }}</strong><br /><span class="muted" style="font-size:12px">{{ p.description ?? '' }}</span></div>
          <button class="secondary small" @click="removeDirectPerm(p.id)">✕</button>
        </div>
      </div>
      <p v-else class="muted">No direct permissions assigned.</p>
    </div>
  </div>
  <div class="container" v-else-if="loading"><p>Loading…</p></div>
  <div class="container" v-else-if="error"><p class="error">{{ error }}</p><button class="secondary" @click="load">Retry</button></div>
</template>

<style scoped>
.back-link { cursor: pointer; color: var(--primary); font-size: 13px; display: inline-block; margin-bottom: 4px; }
.edit-form { display: flex; flex-direction: column; gap: 10px; max-width: 400px; margin-top: 14px; }
.edit-form input { width: 100%; }
.detail-row { display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid var(--border); }
.detail-row:last-child { border: none; }
.label { color: var(--text-dim); font-weight: 500; }
.status-ok { color: var(--success); font-weight: 600; }
.status-muted { color: var(--text-muted); }
.assign-row { display: flex; gap: 8px; margin-bottom: 12px; }
.assign-row select { flex: 1; }
.assigned-item { display: flex; justify-content: space-between; align-items: center; padding: 8px 0; border-bottom: 1px solid var(--border); }
.assigned-item:last-child { border: none; }
.small { padding: 4px 10px; font-size: 12px; }
</style>
