import { describe, it, expect } from "vitest";
import { render, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import DataTable, { type DataTableColumn } from "./DataTable";

interface Row { id: string; code: string; qty: number }

const rows: Row[] = [
  { id: "1", code: "BETA", qty: 30 },
  { id: "2", code: "alpha", qty: 10 },
  { id: "3", code: "gamma", qty: 20 },
];

const columns: DataTableColumn<Row>[] = [
  { key: "code", label: "Código", sortValue: (r) => r.code },
  { key: "qty", label: "Cantidad", sortValue: (r) => r.qty },
];

function setup(extra: Partial<React.ComponentProps<typeof DataTable<Row>>> = {}) {
  return render(
    <DataTable<Row>
      aria-label="Tabla de prueba"
      columns={columns}
      rows={rows}
      getRowKey={(r) => r.id}
      {...extra}
    />
  );
}

const bodyCodes = () =>
  screen.getAllByRole("row").slice(1).map((r) => within(r).getAllByRole("cell")[0].textContent);

describe("DataTable", () => {
  it("renderiza todas las filas inicialmente", () => {
    setup();
    expect(screen.getByText("BETA")).toBeInTheDocument();
    expect(screen.getByText("alpha")).toBeInTheDocument();
    expect(screen.getByText("gamma")).toBeInTheDocument();
  });

  it("la búsqueda filtra por el valor de columna (insensible a mayúsculas)", async () => {
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByRole("textbox"), "alph");
    expect(screen.getByText("alpha")).toBeInTheDocument();
    expect(screen.queryByText("BETA")).not.toBeInTheDocument();
    expect(screen.getByText(/1 de 3 resultado/)).toBeInTheDocument();
  });

  it("ordena ascendente y luego descendente al hacer clic en el encabezado", async () => {
    const user = userEvent.setup();
    setup();
    const header = screen.getByRole("button", { name: /Cantidad/ });

    await user.click(header); // asc por cantidad: 10, 20, 30
    expect(bodyCodes()).toEqual(["alpha", "gamma", "BETA"]);

    await user.click(header); // desc: 30, 20, 10
    expect(bodyCodes()).toEqual(["BETA", "gamma", "alpha"]);
  });

  it("muestra mensaje de vacío cuando no hay coincidencias de búsqueda", async () => {
    const user = userEvent.setup();
    setup();
    await user.type(screen.getByRole("textbox"), "zzz");
    expect(screen.getByText(/Sin resultados para "zzz"/)).toBeInTheDocument();
  });

  it("avisa cuando la vista está truncada respecto al total del servidor", () => {
    setup({ totalOnServer: 500 });
    expect(screen.getByText(/Mostrando 3 de 500 registros/)).toBeInTheDocument();
  });

  it("renderiza acciones de fila cuando se proveen", () => {
    setup({ rowActions: (r) => <button>editar {r.code}</button> });
    expect(screen.getByRole("button", { name: "editar BETA" })).toBeInTheDocument();
  });
});
