import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    host: true, // escucha en la LAN: accesible desde otros equipos de la red
    // Nombres por los que se permite acceder (además de la IP, siempre permitida).
    allowedHosts: ["p-agu-it-f0vw", "p-agu-it-f0vw.sumimedical.ips"],
    port: 5173,
    proxy: {
      "/api": { target: "http://localhost:5080", changeOrigin: true },
      "/hubs": { target: "http://localhost:5080", ws: true, changeOrigin: true },
    },
  },
});
