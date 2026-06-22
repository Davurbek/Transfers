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
    <div class="login-bg" />
    <form class="login-card" @submit.prevent="submit">
      <div class="login-header">
        <div class="login-icon">T</div>
        <h1>Transfers Ops</h1>
        <p class="muted">Sign in to your account</p>
      </div>

      <div class="login-body">
        <label>
          <span>Username</span>
          <div class="input-wrap">
            <span class="input-icon">👤</span>
            <input v-model="username" autocomplete="username" required />
          </div>
        </label>
        <label>
          <span>Password</span>
          <div class="input-wrap">
            <span class="input-icon">🔒</span>
            <input v-model="password" type="password" autocomplete="current-password" required />
          </div>
        </label>

        <p v-if="error" class="error-msg">{{ error }}</p>

        <button type="submit" :disabled="loading" class="login-btn">
          <span v-if="loading" class="spinner" />
          <span>{{ loading ? 'Signing in…' : 'Sign in' }}</span>
        </button>
      </div>

      <div class="login-footer">
        <p class="muted">Demo accounts (password <code>Passw0rd!</code>)</p>
        <div class="accounts">
          <div class="account-row">
            <span class="account-role">support</span>
            <span class="account-desc">— read only</span>
          </div>
          <div class="account-row">
            <span class="account-role">ops</span>
            <span class="account-desc">— read + unpause</span>
          </div>
          <div class="account-row">
            <span class="account-role">compliance</span>
            <span class="account-desc">— read + unpause + audit</span>
          </div>
        </div>
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
  position: relative;
  overflow: hidden;
}
.login-bg {
  position: fixed;
  inset: 0;
  background:
    radial-gradient(ellipse at 20% 50%, rgba(59, 130, 246, 0.12) 0%, transparent 50%),
    radial-gradient(ellipse at 80% 50%, rgba(139, 92, 246, 0.08) 0%, transparent 50%);
  pointer-events: none;
}

.login-card {
  width: 400px;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 16px;
  box-shadow: 0 25px 60px rgba(0, 0, 0, 0.5);
  overflow: hidden;
  position: relative;
  z-index: 1;
  animation: slideUp 0.4s ease;
}
@keyframes slideUp {
  from { opacity: 0; transform: translateY(20px); }
  to { opacity: 1; transform: translateY(0); }
}

.login-header {
  padding: 32px 32px 0;
  text-align: center;
}
.login-icon {
  width: 48px;
  height: 48px;
  background: linear-gradient(135deg, var(--primary), #8b5cf6);
  border-radius: 12px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  font-size: 22px;
  color: white;
  margin-bottom: 16px;
}
.login-header h1 {
  font-size: 20px;
  font-weight: 700;
  margin: 0 0 6px;
}
.login-header p {
  font-size: 13px;
  margin: 0 0 24px;
}

.login-body {
  padding: 0 32px 24px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.login-body label {
  display: flex;
  flex-direction: column;
  gap: 6px;
  font-size: 12px;
  font-weight: 600;
  color: var(--text-dim);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}
.input-wrap {
  position: relative;
}
.input-icon {
  position: absolute;
  left: 12px;
  top: 50%;
  transform: translateY(-50%);
  font-size: 14px;
  pointer-events: none;
}
.input-wrap input {
  width: 100%;
  padding-left: 36px;
}
.login-btn {
  width: 100%;
  justify-content: center;
  padding: 11px;
  font-size: 14px;
  border-radius: var(--radius-sm);
}
.spinner {
  width: 16px;
  height: 16px;
  border: 2px solid rgba(255, 255, 255, 0.3);
  border-top-color: white;
  border-radius: 50%;
  animation: spin 0.6s linear infinite;
}
@keyframes spin {
  to { transform: rotate(360deg); }
}
.error-msg {
  color: var(--danger);
  font-size: 13px;
  font-weight: 500;
  text-align: center;
  padding: 8px 12px;
  background: var(--danger-bg);
  border-radius: var(--radius-sm);
}

.login-footer {
  padding: 16px 32px 24px;
  background: var(--surface-2);
  border-top: 1px solid var(--border);
}
.login-footer p {
  font-size: 11px;
  margin-bottom: 10px;
  text-transform: uppercase;
  letter-spacing: 0.05em;
}
.accounts {
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.account-row {
  display: flex;
  gap: 6px;
  font-size: 12px;
}
.account-role {
  font-family: ui-monospace, 'SF Mono', Menlo, monospace;
  font-weight: 600;
  color: var(--primary);
}
.account-desc {
  color: var(--text-muted);
}
</style>
