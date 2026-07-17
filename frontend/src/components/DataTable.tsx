import { useMemo, useState } from "react";
import {
  Alert, Box, Paper, Table, TableBody, TableCell, TableContainer, TableHead,
  TablePagination, TableRow, TableSortLabel, TextField, Typography,
} from "@mui/material";
import SearchIcon from "@mui/icons-material/Search";
import InputAdornment from "@mui/material/InputAdornment";

export interface DataTableColumn<T> {
  key: string;
  label: string;
  align?: "left" | "center" | "right";
  /** Cómo renderizar la celda. Por defecto usa `sortValue` convertido a texto. */
  render?: (row: T) => React.ReactNode;
  /** Valor usado para ordenar y para la búsqueda instantánea (debe ser texto o número plano). */
  sortValue?: (row: T) => string | number;
  /** Si es false, la columna no participa en la búsqueda global ni en el ordenamiento. */
  sortable?: boolean;
}

interface Props<T> {
  columns: DataTableColumn<T>[];
  rows: T[];
  getRowKey: (row: T) => string;
  /** Acciones de fila (ej. Editar/Eliminar), renderizadas en una columna final sin encabezado de texto. */
  rowActions?: (row: T) => React.ReactNode;
  searchPlaceholder?: string;
  /** Total real en el servidor (para avisar si se truncó la vista, ver Design-System.md §Decisión de datos). */
  totalOnServer?: number;
  emptyMessage?: string;
  "aria-label": string;
}

const DEFAULT_PAGE_SIZE = 10;
const PAGE_SIZE_OPTIONS = [10, 25, 50, 100];

/**
 * Tabla de datos con búsqueda instantánea, ordenamiento por columna y tamaño de página
 * configurable — todo del lado del cliente sobre las filas ya cargadas. Ver Design-System.md
 * (Decisión: catálogos por proyecto, escala de decenas/cientos, no miles de filas) para la
 * justificación de por qué esto reemplaza la paginación/búsqueda server-side en catálogos.
 */
export default function DataTable<T>({
  columns, rows, getRowKey, rowActions, searchPlaceholder = "Buscar…",
  totalOnServer, emptyMessage = "No hay resultados.", ...aria
}: Props<T>) {
  const [search, setSearch] = useState("");
  const [orderBy, setOrderBy] = useState<string | null>(null);
  const [orderDir, setOrderDir] = useState<"asc" | "desc">("asc");
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);

  const filtered = useMemo(() => {
    if (!search.trim()) return rows;
    const q = search.trim().toLowerCase();
    const searchableCols = columns.filter((c) => c.sortValue);
    return rows.filter((row) =>
      searchableCols.some((c) => String(c.sortValue!(row)).toLowerCase().includes(q))
    );
  }, [rows, search, columns]);

  const sorted = useMemo(() => {
    if (!orderBy) return filtered;
    const col = columns.find((c) => c.key === orderBy);
    if (!col?.sortValue) return filtered;
    const dir = orderDir === "asc" ? 1 : -1;
    return [...filtered].sort((a, b) => {
      const va = col.sortValue!(a);
      const vb = col.sortValue!(b);
      if (va < vb) return -1 * dir;
      if (va > vb) return 1 * dir;
      return 0;
    });
  }, [filtered, orderBy, orderDir, columns]);

  const paged = sorted.slice(page * pageSize, page * pageSize + pageSize);

  const handleSort = (key: string) => {
    if (orderBy === key) {
      setOrderDir((d) => (d === "asc" ? "desc" : "asc"));
    } else {
      setOrderBy(key);
      setOrderDir("asc");
    }
    setPage(0);
  };

  return (
    <div className="flex flex-col gap-3">
      <Box className="flex items-center justify-between gap-2">
        <TextField
          size="small"
          placeholder={searchPlaceholder}
          value={search}
          onChange={(e) => { setSearch(e.target.value); setPage(0); }}
          className="max-w-sm w-full"
          slotProps={{
            input: {
              startAdornment: (
                <InputAdornment position="start">
                  <SearchIcon fontSize="small" color="action" />
                </InputAdornment>
              ),
            },
          }}
          aria-label="Buscar en la tabla"
        />
        {search && (
          <Typography variant="caption" color="text.secondary" className="whitespace-nowrap">
            {filtered.length} de {rows.length} resultado(s)
          </Typography>
        )}
      </Box>

      {totalOnServer !== undefined && totalOnServer > rows.length && (
        <Alert severity="info" variant="outlined">
          Mostrando {rows.length} de {totalOnServer} registros. Refine su búsqueda en el
          servidor o contacte a soporte si necesita ver el catálogo completo.
        </Alert>
      )}

      <TableContainer component={Paper}>
        <Table size="small" aria-label={aria["aria-label"]}>
          <TableHead>
            <TableRow>
              {columns.map((col) => (
                <TableCell key={col.key} align={col.align ?? "left"}>
                  {col.sortable === false || !col.sortValue ? (
                    col.label
                  ) : (
                    <TableSortLabel
                      active={orderBy === col.key}
                      direction={orderBy === col.key ? orderDir : "asc"}
                      onClick={() => handleSort(col.key)}
                    >
                      {col.label}
                    </TableSortLabel>
                  )}
                </TableCell>
              ))}
              {rowActions && <TableCell align="right">Acciones</TableCell>}
            </TableRow>
          </TableHead>
          <TableBody>
            {paged.length === 0 ? (
              <TableRow>
                <TableCell colSpan={columns.length + (rowActions ? 1 : 0)} align="center" className="py-8">
                  <Typography color="text.secondary">
                    {search ? `Sin resultados para "${search}".` : emptyMessage}
                  </Typography>
                </TableCell>
              </TableRow>
            ) : (
              paged.map((row) => (
                <TableRow key={getRowKey(row)} hover>
                  {columns.map((col) => (
                    <TableCell key={col.key} align={col.align ?? "left"}>
                      {col.render ? col.render(row) : (col.sortValue?.(row) ?? "—")}
                    </TableCell>
                  ))}
                  {rowActions && <TableCell align="right">{rowActions(row)}</TableCell>}
                </TableRow>
              ))
            )}
          </TableBody>
        </Table>
        <TablePagination
          component="div"
          count={sorted.length}
          page={page}
          onPageChange={(_, newPage) => setPage(newPage)}
          rowsPerPage={pageSize}
          onRowsPerPageChange={(e) => { setPageSize(parseInt(e.target.value, 10)); setPage(0); }}
          rowsPerPageOptions={PAGE_SIZE_OPTIONS}
          labelRowsPerPage="Filas por página:"
          labelDisplayedRows={({ from, to, count }) => `${from}–${to} de ${count}`}
        />
      </TableContainer>
    </div>
  );
}
