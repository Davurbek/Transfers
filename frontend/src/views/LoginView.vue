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
    radial-gradient(ellipse at 20% 50%, rgba(91, 156, 255, 0.15) 0%, transparent 50%),
    radial-gradient(ellipse at 80% 50%, rgba(167, 139, 250, 0.12) 0%, transparent 50%),
    radial-gradient(ellipse at 50% 0%, rgba(244, 114, 182, 0.06) 0%, transparent 50%);
  pointer-events: none;
}

.login-card {
  width: 400px;
  background: var(--surface);
  border: 1px solid var(--surface-border);
  border-radius: 16px;
  box-shadow: 0 25px 60px rgba(0, 0, 0, 0.5);
  overflow: hidden;
  position: relative;
  z-index: 1;
  animation: slideUp 0.4s ease;
  backdrop-filter: blur(16px);
}
@keyframes slideUp {
  from { opacity: 0; transform: translateY(24px); }
  to { opacity: 1; transform: translateY(0); }
}

.login-header {
  padding: 36px 32px 0;
  text-align: center;
}
.login-icon {
  width: 52px;
  height: 52px;
  background: linear-gradient(135deg, #f472b6, var(--purple));
  border-radius: 14px;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-weight: 700;
  font-size: 24px;
  color: white;
  margin-bottom: 18px;
  box-shadow: 0 6px 20px var(--purple-glow);
}
.login-header h1 {
  font-size: 22px;
  font-weight: 800;
  margin: 0 0 6px;
  letter-spacing: -0.03em;
}
.login-header p {
  font-size: 13px;
  margin: 0 0 28px;
}

.login-body {
  padding: 0 32px 24px;
  display: flex;
  flex-direction: column;
  gap: 18px;
}
.login-body label {
  display: flex;
  flex-direction: column;
  gap: 6px;
  font-size: 11px;
  font-weight: 600;
  color: var(--text-dim);
  text-transform: uppercase;
  letter-spacing: 0.06em;
}
.input-wrap {
  position: relative;
}
.input-icon {
  position: absolute;
  left: 13px;
  top: 50%;
  transform: translateY(-50%);
  font-size: 14px;
  pointer-events: none;
  filter: saturate(1.3);
}
.input-wrap input {
  width: 100%;
  padding-left: 38px;
}
.login-btn {
  width: 100%;
  justify-content: center;
  padding: 12px;
  font-size: 14px;
  border-radius: var(--radius-sm);
  margin-top: 4px;
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
  padding: 10px 14px;
  background: var(--danger-bg);
  border: 1px solid rgba(248, 113, 113, 0.2);
  border-radius: var(--radius-sm);
}

.login-footer {
  padding: 18px 32px 24px;
  background: rgba(0, 0, 0, 0.2);
  border-top: 1px solid var(--border);
}
.login-footer p {
  font-size: 10px;
  margin-bottom: 12px;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  font-weight: 600;
}
.accounts {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.account-row {
  display: flex;
  gap: 8px;
  font-size: 12px;
  padding: 4px 8px;
  background: rgba(0, 0, 0, 0.15);
  border-radius: 4px;
}
.account-role {
  font-family: ui-monospace, 'SF Mono', Menlo, monospace;
  font-weight: 700;
  color: var(--primary-hover);
}
.account-desc {
  color: var(--text-muted);
}
</style>
