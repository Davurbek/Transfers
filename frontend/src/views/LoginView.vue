<script setup lang="ts">
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { AxiosError } from 'axios'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

const username = ref('ops')
const password = ref('Passw0rd!')
const error = ref<string | null>(null)
const loading = ref(false)

async function submit() {
  error.value = null
  loading.value = true
  try {
    await auth.login(username.value, password.value)
    const redirect = (route.query.redirect as string) || '/'
    router.push(redirect)
  } catch (e) {
    const ax = e as AxiosError<{ message?: string }>
    error.value =
      ax.response?.status === 429
        ? 'Too many attempts. Please wait and try again.'
        : ax.response?.data?.message ?? 'Login failed'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="login-wrap">
    <form class="card login-card" @submit.prevent="submit">
      <h1>Transfers Ops Dashboard</h1>
      <p class="muted">Sign in to continue</p>

      <label>
        Username
        <input v-model="username" autocomplete="username" required />
      </label>
      <label>
        Password
        <input v-model="password" type="password" autocomplete="current-password" required />
      </label>

      <p v-if="error" class="error">{{ error }}</p>

      <button type="submit" :disabled="loading">
        {{ loading ? 'Signing in…' : 'Sign in' }}
      </button>

      <div class="hint muted">
        <strong>Demo accounts</strong> (password <code>Passw0rd!</code>):
        <ul>
          <li><code>support</code> — read only</li>
          <li><code>ops</code> — read + unpause</li>
          <li><code>compliance</code> — read + unpause + audit</li>
        </ul>
      </div>
    </form>
  </div>
</template>

<style scoped>
.login-wrap {
  min-height: 100vh;
  display: grid;
  place-items: center;
  padding: 20px;
}
.login-card {
  width: 360px;
  display: flex;
  flex-direction: column;
  gap: 12px;
}
h1 {
  font-size: 20px;
  margin: 0;
}
label {
  display: flex;
  flex-direction: column;
  gap: 6px;
  font-size: 13px;
  color: var(--text-dim);
}
.hint {
  font-size: 12px;
  margin-top: 8px;
  border-top: 1px solid var(--border);
  padding-top: 12px;
}
.hint ul {
  margin: 6px 0 0;
  padding-left: 18px;
}
</style>
