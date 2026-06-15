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
}
.card {
  text-align: center;
  max-width: 420px;
}
h1 {
  font-size: 20px;
  margin: 0 0 8px;
}
button {
  margin-top: 16px;
}
</style>
