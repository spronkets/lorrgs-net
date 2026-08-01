import { defineConfig } from 'vite'
import { svelte } from '@sveltejs/vite-plugin-svelte'

// https://vite.dev/config/
export default defineConfig({
  plugins: [svelte()],
  server: {
    proxy: {
      '/health': {
        target: 'http://localhost:5247',
        changeOrigin: true,
        secure: false
      },
      '/api': {
        target: 'http://localhost:5247',
        changeOrigin: true,
        secure: false,
        rewrite: (path) => path
      }
    }
  }
})
