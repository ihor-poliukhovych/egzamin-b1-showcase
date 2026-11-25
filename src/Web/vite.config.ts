import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { execSync } from 'node:child_process'

// short commit hash for the footer; outside a git checkout it stays 'dev'
function buildVersion() {
  // Docker builds have no .git, so the hash arrives as the BUILD_VERSION build argument
  if (process.env.BUILD_VERSION && process.env.BUILD_VERSION !== 'dev') return process.env.BUILD_VERSION
  try {
    return execSync('git rev-parse --short HEAD', { stdio: ['ignore', 'pipe', 'ignore'] }).toString().trim() || 'dev'
  } catch {
    return 'dev'
  }
}

const api = process.env.VITE_API_URL ?? 'http://localhost:5080'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  define: { __BUILD__: JSON.stringify(buildVersion()) },
  server: {
    port: 5173,
    proxy: {
      '/today': api,
      '/attempts': api,
      '/patterns': api,
      '/arkusze': api,
      '/exam-attempts': api,
      '/exam-sessions': api,
      '/auth': api,
      '/audio': api,
      '/groups': api,
      '/join': api,
      '/signin-google': api,
      '/checklists': api,
      '/tools': api,
      '/site': api,
      '/dashboard': api,
      '/teacher': api,
      '/feedback': api,
      '/media': api,
    },
  },
})
