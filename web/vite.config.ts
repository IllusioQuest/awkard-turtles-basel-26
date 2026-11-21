import react from '@vitejs/plugin-react'
import { defineConfig, searchForWorkspaceRoot } from 'vite'
import { readFileSync } from 'node:fs'
const monitorPolicy = JSON.parse(readFileSync(new URL('../gaming/deploy/monitor-api.json', import.meta.url), 'utf8')) as { port: number; frontend_port: number }

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  // The calculator reads sourced data straight from the repository's data/raw folder.
  server: {
    host: '127.0.0.1', port: monitorPolicy.frontend_port, strictPort: true,
    allowedHosts: ['localhost', '127.0.0.1'],
    // The more specific monitor route must come before the fleet-results catch-all /api.
    proxy: {
      '/api/monitor': { target: `http://127.0.0.1:${monitorPolicy.port}`, changeOrigin: true },
      // Fleet-results API: dotnet run --project src/DigitalHeat.FleetApi
      '/api': 'http://localhost:5080',
    },
    fs: { allow: [searchForWorkspaceRoot(process.cwd()), '../data/raw'] },
  },
})
