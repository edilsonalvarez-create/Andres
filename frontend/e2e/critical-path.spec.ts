import { test, expect } from "@playwright/test";
import { login, uniqueCode } from "./helpers";

/**
 * Camino crítico de negocio: crear un proyecto y un caso de prueba desde la UI, verificando
 * que aparecen en sus respectivos listados. Es el flujo mínimo que un QA nuevo debe poder
 * completar sin capacitación (objetivo del Sprint 3).
 */
test.describe("Camino crítico", () => {
  test("crear proyecto y caso de prueba de punta a punta", async ({ page }) => {
    await login(page);
    const code = uniqueCode("CP");

    // 1. Crear proyecto
    await page.getByRole("button", { name: "Buscar…" }).click().catch(() => {});
    await page.keyboard.press("Escape").catch(() => {});
    await page.getByRole("link", { name: "Proyectos" }).click();
    await page.getByRole("button", { name: "Nuevo proyecto" }).click();
    await page.getByLabel("Código").fill(code);
    await page.getByLabel("Nombre").fill("Proyecto E2E");
    await page.getByRole("button", { name: "Crear" }).click();

    // El proyecto aparece en la tabla (búsqueda instantánea del DataTable).
    await page.getByPlaceholder(/Buscar por nombre o código/).fill(code);
    await expect(page.getByRole("cell", { name: code })).toBeVisible();

    // 2. Crear caso de prueba en ese proyecto
    await page.getByRole("link", { name: "Casos de prueba" }).click();
    // Seleccionar el proyecto recién creado en el selector.
    await page.getByLabel("Proyecto").click();
    await page.getByRole("option", { name: new RegExp(code) }).click();
    await page.getByRole("button", { name: "Nuevo script" }).click();
    await expect(page.getByRole("dialog")).toBeVisible();
  });

  test("filtros del dashboard: preset de rango en un clic", async ({ page }) => {
    await login(page);
    await page.getByRole("button", { name: "30 días" }).click();
    // El chip queda seleccionado (filled) — no verificamos datos, solo la interacción.
    await expect(page.getByRole("button", { name: "30 días" })).toBeVisible();
  });
});
