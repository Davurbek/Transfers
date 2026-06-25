<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { adminService } from '@/di/container'
import type { PermissionInfo, PermissionCreate } from '@/domain/models'

const permissions = ref<PermissionInfo[]>([])
const loading = ref(true)
const error = ref('')
const showCreate = ref(false)
const creating = ref(false)
const newPerm = ref<PermissionCreate>({ code: '', description: '' })
const createError = ref('')
const editingId = ref<string | null>(null)
const editForm = ref<PermissionCreate>({ code: '', description: '' })
const saving = ref(false)

async function load() {
  loading.value = true
  error.value = ''
  try {
    permissions.value = await adminService.getAllPermissions()
  } catch (e: any) {
    error.value = e?.response?.data?.message || e.message || 'Failed to load permissions'
  } finally {
    loading.value = false
  }
}

async function handleCreate() {
  creating.value = true
  createError.value = ''
  try {
    await adminService.createPermission(newPerm.value)
    showCreate.value = false
    newPerm.value = { code: '', description: '' }
    await load()
  } catch (e: any) {
    createError.value = e?.response?.data?.message || e.message || 'Failed to create permission'
  } finally {
    creating.value = false
  }
}

function startEdit(p: PermissionInfo) {
  editingId.value = p.id
  editForm.value = { code: p.code, description: p.description ?? '' }
}

async function saveEdit(id: string) {
  saving.value = true
  try {
    await adminService.updatePermission(id, editForm.value)
    editingId.value = null
    await load()
  } catch (e: any) {
    error.value = e?.response?.data?.message || e.message || 'Failed to update'
  } finally {
    saving.value = false
  }
}

async function deletePermission(id: string) {
  if (!confirm('Delete this permission?')) return
  try {
    await adminService.deletePermission(id)
    await load()
  } catch (e: any) {
    error.value = e?.response?.data?.message || e.message || 'Failed to delete'
  }
}

onMounted(load)
</script>

<template>
  <div class="container">
    <div class="page-header">
      <div>
        <h1>Permissions</h1>
        <p class="muted" v-if="!loading">{{ permissions.length }} permissions</p>
      </div>
      <button @click="showCreate = !showCreate">{{ showCreate ? 'Cancel' : '+ New Permission' }}</button>
    </div>

    <div class="card" v-if="showCreate">
      <h3>Create Permission</h3>
      <form class="create-form" @submit.prevent="handleCreate">
        <input v-model="newPerm.code" placeholder="Code (e.g. tx:read)" required />
        <input v-model="newPerm.description" placeholder="Description" />
        <p class="error" v-if="createError">{{ createError }}</p>
        <button type="submit" :disabled="creating">{{ creating ? 'Creating…' : 'Create' }}</button>
      </form>
    </div>

    <div class="card table-card">
      <div v-if="error" class="error-state"><p class="error">{{ error }}</p><button class="secondary" @click="load">Retry</button></div>
      <template v-else-if="loading">
        <div class="skeleton-row" v-for="i in 3" :key="i"><div class="skeleton" style="width:20%;height:14px" /><div class="skeleton" style="width:50%;height:14px" /><div class="skeleton" style="width:10%;height:14px" /></div>
      </template>
      <template v-else>
        <div class="table-wrap">
          <table>
            <thead><tr><th>Code</th><th>Description</th><th></th></tr></thead>
            <tbody>
              <tr v-for="p in permissions" :key="p.id">
                <td v-if="editingId !== p.id"><strong>{{ p.code }}</strong></td>
                <td v-if="editingId !== p.id" class="muted">{{ p.description ?? '—' }}</td>
                <td v-if="editingId !== p.id" class="actions">
                  <button class="secondary small" @click="startEdit(p)">Edit</button>
                  <button class="secondary small danger" @click="deletePermission(p.id)">✕</button>
                </td>
                <td v-if="editingId === p.id" colspan="3">
                  <form class="inline-edit" @submit.prevent="saveEdit(p.id)">
                    <input v-model="editForm.code" required />
                    <input v-model="editForm.description" />
                    <button type="submit" :disabled="saving">Save</button>
                    <button class="secondary" @click="editingId = null">Cancel</button>
                  </form>
                </td>
              </tr>
              <tr v-if="!permissions.length"><td colspan="3" class="empty-state">No permissions found.</td></tr>
            </tbody>
          </table>
        </div>
      </template>
    </div>
  </div>
</template>

<style scoped>
.create-form { display: flex; flex-direction: column; gap: 10px; margin-top: 14px; max-width: 400px; }
.create-form input { width: 100%; }
.actions { display: flex; gap: 6px; }
.small { padding: 4px 10px; font-size: 12px; }
.inline-edit { display: flex; gap: 6px; flex-wrap: wrap; }
.inline-edit input { flex: 1; min-width: 120px; }
</style>
