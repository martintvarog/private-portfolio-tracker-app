import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // The browser only ever talks to the Vite origin; Vite forwards /api/*
    // to the ASP.NET Core host, so no CORS is needed in dev. In production
    // the client is served from the same origin as the API.
    proxy: {
      '/api': {
        target: 'http://localhost:5018'
      },
    },
  },
})
