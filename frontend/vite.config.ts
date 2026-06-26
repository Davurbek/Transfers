import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 5173,
    proxy: {
      // Proxy API calls to the .NET backend during development.
      // The httpClient uses /api prefix; rewrite strips it since backend routes
      // are at /auth, /transactions, /audit, /admin (no /api prefix).
      '/api': {
        target: 'http://localhost:5290',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
    },
  },
})
