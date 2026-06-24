<script setup lang="ts">
import { useRouter } from 'vue-router'
const router = useRouter()
</script>

<template>
  <div class="container center">
    <div class="card">
      <h1>403 — Forbidden</h1>
      <p class="muted">You don't have permission to view this page.</p>
      <button @click="router.push('/')">Back to dashboard</button>
    </div>
  </div>
</template>

<style scoped>
.center {
  min-height: 70vh;
  display: grid;
  place-items: center;
  padding: 40px;
}
.card {
  text-align: center;
  max-width: 420px;
  padding: 48px 36px;
}
h1 {
  font-size: 22px;
  margin: 0 0 8px;
  font-weight: 800;
  letter-spacing: -0.02em;
}
p {
  font-size: 14px;
}
button {
  margin-top: 20px;
}
</style>
