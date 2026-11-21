import react from '@vitejs/plugin-react'
import { defineConfig, searchForWorkspaceRoot } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  // The calculator reads sourced data straight from the repository's data/raw folder.
  server: { fs: { allow: [searchForWorkspaceRoot(process.cwd()), '../data/raw'] } },
})
