<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { adminService } from '@/di/container'
import type { RoleDetail, PermissionInfo, RoleCreate } from '@/domain/models'

const route = useRoute()
const router = useRouter()
const roleId = route.params.id as string

const role = ref<RoleDetail | null>(null)
const allPermissions = ref<PermissionInfo[]>([])
const loading = ref(true)
const error = ref('')
const editing = ref(false)
const editForm = ref<RoleCreate>({ name: '', description: '' })
const saving = ref(false)
const selectedPermId = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    const [r, perms] = await Promise.all([
      adminService.getRole(roleId),
      adminService.getAllPermissions(),
    ])
    role.value = r
    allPermissions.value = perms
    editForm.value = { name: r.name, description: r.description ?? '' }
  } catch (e: any) {
    error.value = e?.response?.data?.message || e.message || 'Role not found'
  } finally {
    loading.value = false
  }
}

async function saveRole() {
  saving.value = true
  try {
    role.value = await adminService.updateRole(roleId, editForm.value)
    editing.value = false
  } catch (e: any) {
    error.value = e?.response?.data?.message || e.message || 'Failed to update'
  } finally {
    saving.value = false
  }
}

async function addPermission() {
  if (!selectedPermId.value) return
  try {
    await adminService.addRolePermission(roleId, selectedPermId.value)
    selectedPermId.value = ''
    await load()
  } catch (e: any) {
    error.value = e?.response?.data?.message || e.message || 'Failed to add permission'
  }
}

async function removePermission(permId: string) {
  try {
    await adminService.removeRolePermission(roleId, permId)
    await load()
  } catch (e: any) {
    error.value = e?.response?.data?.message || e.message || 'Failed to remove permission'
  }
}

async function deleteRole() {
  if (!confirm('Delete this role? This will remove it from all users.')) return
  try {
    await adminService.deleteRole(roleId)
    router.push({ name: 'roles' })
  } catch (e: any) {
    error.value = e?.response?.data?.message || e.message || 'Failed to delete'
  }
}

const availablePermissions = computed(() =>
  allPermissions.value.filter(p => !role.value?.permissions.some(rp => rp.id === p.id))
)

onMounted(load)
</script>

<template>
  <div class="container" v-if="!loading && role">
    <div class="page-header">
      <div>
        <a class="back-link" @click="router.push({ name: 'roles' })">← Roles</a>
        <h1>{{ role.name }}</h1>
      </div>
      <div class="row">
        <button class="secondary" @click="editing = !editing">{{ editing ? 'Cancel' : 'Edit' }}</button>
        <button class="danger" @click="deleteRole">Delete</button>
      </div>
    </div>

    <div class="card" v-if="editing">
      <h3>Edit Role</h3>
      <form class="edit-form" @submit.prevent="saveRole">
        <input v-model="editForm.name" placeholder="Name" required />
        <input v-model="editForm.description" placeholder="Description" />
        <button type="submit" :disabled="saving">{{ saving ? 'Saving…' : 'Save' }}</button>
      </form>
    </div>

    <div class="card">
      <div class="detail-row"><span class="label">Description</span><span>{{ role.description ?? '—' }}</span></div>
    </div>

    <div class="card">
      <h3>Permissions</h3>
      <div class="assign-row">
        <select v-model="selectedPermId">
          <option value="">Add permission…</option>
          <option v-for="p in availablePermissions" :key="p.id" :value="p.id">{{ p.code }}</option>
        </select>
        <button :disabled="!selectedPermId" @click="addPermission">Add</button>
      </div>
      <div v-if="(role.permissions ?? []).length">
        <div v-for="p in (role.permissions ?? [])" :key="p.id" class="assigned-item">
          <div><strong>{{ p.code }}</strong><br /><span class="muted" style="font-size:12px">{{ p.description ?? '' }}</span></div>
          <button class="secondary small" @click="removePermission(p.id)">✕</button>
        </div>
      </div>
      <p v-else class="muted">No permissions assigned.</p>
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
.assign-row { display: flex; gap: 8px; margin-bottom: 12px; }
.assign-row select { flex: 1; }
.assigned-item { display: flex; justify-content: space-between; align-items: center; padding: 8px 0; border-bottom: 1px solid var(--border); }
.assigned-item:last-child { border: none; }
.small { padding: 4px 10px; font-size: 12px; }
</style>
