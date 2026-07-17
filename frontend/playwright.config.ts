import { defineConfig, devices } from "@playwright/test";

/**
 * Configuración E2E de QA Guardian.
 *
 * Requisitos para ejecutar (`npx playwright test`):
 *  1. La API debe estar corriendo en http://localhost:5080 (`dotnet run --project src/QAGuardian.API`).
 *  2. Navegadores instalados una vez: `npx playwright install chromium`.
 *
 * El frontend lo levanta Playwright automáticamente (webServer). Credenciales de prueba: las
 * del seed de desarrollo (E2E_EMAIL / E2E_PASSWORD, con fallback al admin local de dev).
 */
export default defineConfig({
  testDir: "./e2e",
  testMatch: "**/*.spec.ts",
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: [["list"], ["html", { open: "never" }]],
  use: {
    baseURL: process.env.E2E_BASE_URL ?? "http://localhost:5173",
    trace: "on-first-retry",
    screenshot: "only-on-failure",
  },
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] } },
  ],
  // Levanta el frontend de dev automáticamente; la API debe estar arriba por separado (ver arriba).
  webServer: {
    command: "npm run dev",
    url: "http://localhost:5173",
    reuseExistingServer: !process.env.CI,
    timeout: 120_000,
  },
});
