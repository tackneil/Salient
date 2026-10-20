import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The API runs on the "http" launch profile of Salient.Api; override with SALIENT_API_URL if needed.
const apiUrl = process.env.SALIENT_API_URL ?? 'http://localhost:5139'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': apiUrl,
      '/health': apiUrl,
      '/hubs': { target: apiUrl, ws: true },
    },
  },
})
