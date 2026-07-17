import { test, expect } from "@playwright/test";
import { login } from "./helpers";

/**
 * Smoke: los caminos que, si fallan, indican que el despliegue está roto y no vale la pena
 * seguir probando. Rápidos y sin datos de escritura.
 */
test.describe("Smoke", () => {
  test("la página de login carga y es accesible", async ({ page }) => {
    await page.goto("/login");
    await expect(page.getByRole("heading", { name: "QA Guardian" })).toBeVisible();
    await expect(page.getByLabel("Correo electrónico")).toBeVisible();
    // El toggle de contraseña (a11y del Sprint 3) está presente.
    await expect(page.getByRole("button", { name: /Mostrar contraseña/ })).toBeVisible();
  });

  test("credenciales inválidas muestran un error accesible", async ({ page }) => {
    await page.goto("/login");
    // Cuenta inexistente a propósito: no se debe golpear la cuenta admin real (evita el
    // bloqueo por 5 intentos fallidos del Sprint 2 y mantiene el suite idempotente).
    await page.getByLabel("Correo electrónico").fill(`nadie-${Date.now()}@qaguardian.local`);
    await page.getByLabel(/^Contraseña/).fill("clave-incorrecta");
    await page.getByRole("button", { name: "Iniciar sesión" }).click();
    await expect(page.getByRole("alert")).toContainText(/Credenciales inválidas/);
  });

  test("login exitoso llega al dashboard con KPIs", async ({ page }) => {
    await login(page);
    // Encabezados de grupo de KPIs (Sprint 3). "Calidad" exacto para no chocar con "Índice de calidad".
    await expect(page.getByText("Calidad", { exact: true })).toBeVisible();
    await expect(page.getByText("Riesgo y seguridad")).toBeVisible();
  });

  test("la paleta de comandos (Ctrl+K) permite navegar", async ({ page }) => {
    await login(page);
    await page.keyboard.press("Control+k");
    await page.getByPlaceholder(/Ir a/).fill("defect");
    await page.keyboard.press("Enter");
    await expect(page.getByRole("heading", { name: "Gestión de defectos" })).toBeVisible();
  });
});
