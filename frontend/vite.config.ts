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
      // Piso de cobertura (gate de regresión, Sprint 16-C): real ~3% líneas / ~54% branch
      // (2026-07-17, `npm run test:coverage`); la mayoría de páginas aún no tiene pruebas
      // de componente. Umbral honesto con margen menor — sube a medida que crezca la suite.
      thresholds: {
        lines: 2.5,
        statements: 2.5,
        functions: 20,
        branches: 45,
      },
    },
  },
});
