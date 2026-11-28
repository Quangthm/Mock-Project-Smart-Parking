import { defineConfig } from "vite"

import react from "@vitejs/plugin-react"

import tailwindcss from "@tailwindcss/vite"

import { readFile } from "node:fs/promises"

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
    {
      name: "smartpark-dev-db-reset",
      configureServer(server) {
        const markerPath = path.join(__dirname, ".cache", "db-reset-id")
        server.middlewares.use((request, response, next) => {
          if (request.method !== "GET" || request.url !== "/__smartpark/db-reset-id") {
            return next()
          }

          response.setHeader("Cache-Control", "no-store")
          response.setHeader("Content-Type", "text/plain; charset=utf-8")
          void readFile(markerPath, "utf8").then(
            (resetId) => response.end(resetId),
            () => {
              response.statusCode = 404
              response.end()
            },
          )
        })
      },
    },
    react(),

    tailwindcss(),
  ],

  resolve: {
    alias: {
      "@": path.resolve(__dirname, "./src"),
    },
  },
})
