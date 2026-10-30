import { defineConfig } from "vite"

import react from "@vitejs/plugin-react"

import tailwindcss from "@tailwindcss/vite"

import path from "node:path"

import { fileURLToPath } from "node:url"

const __dirname = path.dirname(fileURLToPath(import.meta.url))

export default defineConfig({
  server: {
    proxy: {
      "/payment-api": {
        target: process.env.PAYMENT_SERVICE_PROXY_TARGET ?? "http://localhost:5065",
        changeOrigin: true,
        rewrite: (url) => url.replace(/^\/payment-api/, ""),
      },
    },
  },
  plugins: [
    react(),

    tailwindcss(),
  ],

  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
})
