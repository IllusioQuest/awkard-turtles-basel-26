import react from '@vitejs/plugin-react'
import { defineConfig, searchForWorkspaceRoot } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  // The calculator reads sourced data straight from the repository's data/raw folder.
  server: {
    host: '127.0.0.1', port: 5173, strictPort: true,
    allowedHosts: ['localhost', '127.0.0.1'],
    proxy: {
      // Fleet-results API: dotnet run --project src/DigitalHeat.FleetApi
      '/api': 'http://localhost:5080',
    },
    fs: { allow: [searchForWorkspaceRoot(process.cwd()), '../data/raw'] },
  },
})
