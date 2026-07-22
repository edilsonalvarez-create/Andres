import { describe, it, expect, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import ConfirmDialog from "./ConfirmDialog";

const base = {
  open: true,
  title: "Eliminar proyecto",
  description: "Esta acción no se puede deshacer.",
  onConfirm: vi.fn(),
  onCancel: vi.fn(),
};

describe("ConfirmDialog", () => {
  it("muestra título y descripción cuando está abierto", () => {
    render(<ConfirmDialog {...base} />);
    expect(screen.getByText("Eliminar proyecto")).toBeInTheDocument();
    expect(screen.getByText("Esta acción no se puede deshacer.")).toBeInTheDocument();
  });

  it("no renderiza contenido cuando open=false", () => {
    render(<ConfirmDialog {...base} open={false} />);
    expect(screen.queryByText("Eliminar proyecto")).not.toBeInTheDocument();
  });

  it("invoca onConfirm al confirmar", async () => {
    const user = userEvent.setup();
    const onConfirm = vi.fn();
    render(<ConfirmDialog {...base} confirmLabel="Eliminar" onConfirm={onConfirm} />);
    await user.click(screen.getByRole("button", { name: "Eliminar" }));
    expect(onConfirm).toHaveBeenCalledOnce();
  });

  it("invoca onCancel al cancelar", async () => {
    const user = userEvent.setup();
    const onCancel = vi.fn();
    render(<ConfirmDialog {...base} onCancel={onCancel} />);
    await user.click(screen.getByRole("button", { name: "Cancelar" }));
    expect(onCancel).toHaveBeenCalledOnce();
  });

  it("el botón destructivo usa color de error", () => {
    render(<ConfirmDialog {...base} destructive confirmLabel="Eliminar" />);
    const btn = screen.getByRole("button", { name: "Eliminar" });
    expect(btn.className).toMatch(/colorError/);
  });
});
