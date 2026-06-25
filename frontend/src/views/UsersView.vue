<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import { adminService } from '@/di/container'
import type { UserListItem, UserCreate } from '@/domain/models'

const router = useRouter()
const users = ref<UserListItem[]>([])
const loading = ref(true)
const error = ref('')
const showCreate = ref(false)
const creating = ref(false)
const newUser = ref<UserCreate>({ username: '', email: '', password: '' })
const createError = ref('')

async function load() {
  loading.value = true
  error.value = ''
  try {
    users.value = await adminService.getAllUsers()
  } catch (e: any) {
    error.value = e?.response?.data?.message || e.message || 'Failed to load users'
  } finally {
    loading.value = false
  }
}

async function handleCreate() {
  creating.value = true
  createError.value = ''
  try {
    const user = await adminService.createUser(newUser.value)
    showCreate.value = false
    newUser.value = { username: '', email: '', password: '' }
    await load()
    router.push({ name: 'user-detail', params: { id: user.id } })
  } catch (e: any) {
    createError.value = e?.response?.data?.message || e.message || 'Failed to create user'
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
        <h1>Users</h1>
        <p class="muted" v-if="!loading">{{ users.length }} users</p>
      </div>
      <button @click="showCreate = !showCreate">{{ showCreate ? 'Cancel' : '+ New User' }}</button>
    </div>

    <div class="card" v-if="showCreate">
      <h3>Create User</h3>
      <form class="create-form" @submit.prevent="handleCreate">
        <input v-model="newUser.username" placeholder="Username" required />
        <input v-model="newUser.email" type="email" placeholder="Email" required />
        <input v-model="newUser.password" type="password" placeholder="Password" required />
        <p class="error" v-if="createError">{{ createError }}</p>
        <button type="submit" :disabled="creating">{{ creating ? 'Creating…' : 'Create' }}</button>
      </form>
    </div>

    <div class="card table-card">
      <div v-if="error" class="error-state"><p class="error">{{ error }}</p><button class="secondary" @click="load">Retry</button></div>
      <template v-else-if="loading">
        <div class="skeleton-row" v-for="i in 4" :key="i">
          <div class="skeleton" style="width:20%;height:14px" />
          <div class="skeleton" style="width:25%;height:14px" />
          <div class="skeleton" style="width:10%;height:14px" />
          <div class="skeleton" style="width:20%;height:14px" />
        </div>
      </template>
      <template v-else>
        <div class="table-wrap">
          <table>
            <thead><tr><th>Username</th><th>Email</th><th>Active</th><th>Roles</th><th>Created</th></tr></thead>
            <tbody>
              <tr v-for="u in users" :key="u.id" @click="router.push({ name: 'user-detail', params: { id: u.id } })" class="clickable-row">
                <td>{{ u.username }}</td>
                <td class="muted">{{ u.email }}</td>
                <td><span :class="u.isActive ? 'status-ok' : 'status-muted'">{{ u.isActive ? 'Yes' : 'No' }}</span></td>
                <td><span class="role-tags"><span v-for="r in (u.roles ?? [])" :key="r" class="role-tag">{{ r }}</span><span v-if="!(u.roles ?? []).length" class="muted">—</span></span></td>
                <td class="muted">{{ new Date(u.createdAt).toLocaleDateString() }}</td>
              </tr>
              <tr v-if="!users.length"><td colspan="5" class="empty-state">No users found.</td></tr>
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
.role-tags { display: flex; gap: 4px; flex-wrap: wrap; }
.role-tag { background: var(--primary-subtle); color: var(--primary); padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 600; }
.status-ok { color: var(--success); font-weight: 600; }
.status-muted { color: var(--text-muted); }
</style>
