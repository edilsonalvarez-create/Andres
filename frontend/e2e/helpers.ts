import { Page, expect } from "@playwright/test";

export const CREDENTIALS = {
  email: process.env.E2E_EMAIL ?? "admin@qaguardian.local",
  password: process.env.E2E_PASSWORD ?? "QaGuardian.2026!",
};

/** Inicia sesión por la UI y espera a aterrizar en el dashboard. */
export async function login(page: Page) {
  await page.goto("/login");
  await page.getByLabel("Correo electrónico").fill(CREDENTIALS.email);
  await page.getByLabel(/^Contraseña/).fill(CREDENTIALS.password);
  await page.getByRole("button", { name: "Iniciar sesión" }).click();
  await expect(page.getByRole("heading", { name: "Dashboard ejecutivo" })).toBeVisible({ timeout: 15_000 });
}

/** Código de proyecto único para no colisionar entre corridas. */
export const uniqueCode = (prefix = "E2E") =>
  `${prefix}${Date.now().toString().slice(-6)}`;
