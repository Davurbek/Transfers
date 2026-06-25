<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { adminService } from '@/di/container'
import type { RoleListItem, RoleCreate } from '@/domain/models'

const router = useRouter()
const roles = ref<RoleListItem[]>([])
const loading = ref(true)
const error = ref('')
const showCreate = ref(false)
const creating = ref(false)
const newRole = ref<RoleCreate>({ name: '', description: '' })
const createError = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    roles.value = await adminService.getAllRoles()
  } catch (e: any) {
    error.value = e?.response?.data?.message || e.message || 'Failed to load roles'
  } finally {
    loading.value = false
  }
}

async function handleCreate() {
  creating.value = true
  createError.value = ''
  try {
    const role = await adminService.createRole(newRole.value)
    showCreate.value = false
    newRole.value = { name: '', description: '' }
    await load()
    router.push({ name: 'role-detail', params: { id: role.id } })
  } catch (e: any) {
    createError.value = e?.response?.data?.message || e.message || 'Failed to create role'
  } finally {
    creating.value = false
  }
}

onMounted(load)
</script>

<template>
  <div class="container">
    <div class="page-header">
      <div>
        <h1>Roles</h1>
        <p class="muted" v-if="!loading">{{ roles.length }} roles</p>
      </div>
      <button @click="showCreate = !showCreate">{{ showCreate ? 'Cancel' : '+ New Role' }}</button>
    </div>

    <div class="card" v-if="showCreate">
      <h3>Create Role</h3>
      <form class="create-form" @submit.prevent="handleCreate">
        <input v-model="newRole.name" placeholder="Role name" required />
        <input v-model="newRole.description" placeholder="Description" />
        <p class="error" v-if="createError">{{ createError }}</p>
        <button type="submit" :disabled="creating">{{ creating ? 'Creating…' : 'Create' }}</button>
      </form>
    </div>

    <div class="card table-card">
      <div v-if="error" class="error-state"><p class="error">{{ error }}</p><button class="secondary" @click="load">Retry</button></div>
      <template v-else-if="loading">
        <div class="skeleton-row" v-for="i in 3" :key="i"><div class="skeleton" style="width:30%;height:14px" /><div class="skeleton" style="width:50%;height:14px" /></div>
      </template>
      <template v-else>
        <div class="table-wrap">
          <table>
            <thead><tr><th>Name</th><th>Description</th></tr></thead>
            <tbody>
              <tr v-for="r in roles" :key="r.id" @click="router.push({ name: 'role-detail', params: { id: r.id } })" class="clickable-row">
                <td><strong>{{ r.name }}</strong></td><td class="muted">{{ r.description ?? '—' }}</td>
              </tr>
              <tr v-if="!roles.length"><td colspan="2" class="empty-state">No roles found.</td></tr>
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
.clickable-row { cursor: pointer; }
</style>
