import react from '@vitejs/plugin-react'
import { defineConfig, searchForWorkspaceRoot } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  // The calculator reads sourced data straight from the repository's data/raw folder.
  server: {
    host: '127.0.0.1', port: 5173, strictPort: true,
    // ngrok hosts: the T-78 /speak page is opened on a phone through an https tunnel.
    allowedHosts: ['localhost', '127.0.0.1', '.ngrok-free.app', '.ngrok-free.dev', '.ngrok.app', '.ngrok.dev', '.ngrok.io'],
    proxy: {
      // Fleet-results API: dotnet run --project src/DigitalHeat.FleetApi
      '/api': 'http://localhost:5080',
    },
    fs: { allow: [searchForWorkspaceRoot(process.cwd()), '../data/raw'] },
  },
})
