import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react-swc'
import { TanStackRouterVite } from '@tanstack/router-vite-plugin'

export default defineConfig({
  plugins: [
    react(),
    TanStackRouterVite({
      // Opciones del plugin si las necesitas
    })
  ],
  server: {
    proxy: {
      '/api':  { target: 'https://localhost:7102', changeOrigin: true, secure: false },
      '/hubs': { target: 'https://localhost:7102', changeOrigin: true, secure: false, ws: true },
    },
  },
})