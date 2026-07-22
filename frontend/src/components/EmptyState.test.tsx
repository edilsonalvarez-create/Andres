import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import EmptyState from "./EmptyState";

describe("EmptyState", () => {
  it("la variante error muestra el botón Reintentar y lo invoca", async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    render(<EmptyState variant="error" onRetry={onRetry} />);

    const btn = screen.getByRole("button", { name: /Reintentar/ });
    await user.click(btn);
    expect(onRetry).toHaveBeenCalledOnce();
  });

  it("la variante error sin onRetry no muestra botón", () => {
    render(<EmptyState variant="error" />);
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });

  it("no-selection es un status accesible (aria-live=polite)", () => {
    render(<EmptyState variant="no-selection" />);
    const region = screen.getByRole("status");
    expect(region).toHaveAttribute("aria-live", "polite");
  });

  it("error se anuncia como alerta asertiva", () => {
    render(<EmptyState variant="error" />);
    const region = screen.getByRole("alert");
    expect(region).toHaveAttribute("aria-live", "assertive");
  });

  it("permite sobrescribir título y descripción", () => {
    render(<EmptyState variant="no-data" title="Nada aquí" description="Cree el primero" />);
    expect(screen.getByText("Nada aquí")).toBeInTheDocument();
    expect(screen.getByText("Cree el primero")).toBeInTheDocument();
  });
});
