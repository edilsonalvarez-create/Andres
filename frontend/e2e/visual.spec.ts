import { test, expect } from "@playwright/test";
import { login } from "./helpers";

/**
 * Regresión visual: captura pantallas de referencia y falla si un cambio altera la UI de forma
 * inesperada. La primera corrida crea las baselines (`--update-snapshots`); las siguientes
 * comparan píxel a píxel. Se enmascara/estabiliza contenido dinámico (fechas, números).
 */
test.describe("Regresión visual", () => {
  test("login", async ({ page }) => {
    await page.goto("/login");
    await expect(page.getByRole("heading", { name: "QA Guardian" })).toBeVisible();
    await expect(page).toHaveScreenshot("login.png", { maxDiffPixelRatio: 0.02 });
  });

  test("login en modo oscuro", async ({ page }) => {
    await page.emulateMedia({ colorScheme: "dark" });
    await page.goto("/login");
    await expect(page.getByRole("heading", { name: "QA Guardian" })).toBeVisible();
    await expect(page).toHaveScreenshot("login-dark.png", { maxDiffPixelRatio: 0.02 });
  });

  test("dashboard (contenido dinámico enmascarado)", async ({ page }) => {
    await login(page);
    // Los valores de KPI y gráficos cambian con los datos: se enmascaran para una baseline estable.
    await expect(page).toHaveScreenshot("dashboard.png", {
      mask: [page.locator(".recharts-responsive-container"), page.locator(".MuiCard-root")],
      maxDiffPixelRatio: 0.05,
    });
  });
});
