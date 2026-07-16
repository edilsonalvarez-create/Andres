/// <reference types="vitest/config" />
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  build: {
    rollupOptions: {
      output: {
        manualChunks: {
          vendor: ["react", "react-dom", "react-router-dom"],
          mui: ["@mui/material", "@mui/icons-material", "@emotion/react", "@emotion/styled"],
          charts: ["recharts"],
          signalr: ["@microsoft/signalr"],
        },
      },
    },
    chunkSizeWarningLimit: 600,
  },
  server: {
    host: true, // escucha en la LAN: accesible desde otros equipos de la red
    // Nombres por los que se permite acceder (además de la IP, siempre permitida).
    // El prefijo "." habilita el dominio completo y cualquier equipo de la red corporativa.
    allowedHosts: ["p-agu-it-f0vw", ".sumimedical.ips", ".sumimedical.local"],
    port: 5173,
    proxy: {
      "/api": { target: "http://localhost:5080", changeOrigin: true },
      "/hubs": { target: "http://localhost:5080", ws: true, changeOrigin: true },
    },
  },
  test: {
    globals: true,
    environment: "jsdom",
    setupFiles: ["./src/test/setup.ts"],
    css: false,
    // Vitest solo corre pruebas unitarias/componente en src (.test); los .spec de e2e son de Playwright.
    include: ["src/**/*.test.{ts,tsx}"],
    exclude: ["e2e/**", "node_modules/**"],
    coverage: {
      provider: "v8",
      reporter: ["text", "json-summary"],
      // Solo el código propio: se excluyen entradas, tipos y el propio andamiaje de pruebas.
      include: ["src/**/*.{ts,tsx}"],
      exclude: ["src/main.tsx", "src/**/*.d.ts", "src/test/**", "src/**/*.test.{ts,tsx}"],
    },
  },
});
