import { useEffect, useState } from "react";
import {
  Alert, Box, Paper, Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  TextField, Typography,
} from "@mui/material";
import { api } from "../api/client";
import type { Paged } from "../types";

interface AuditEntry {
  id: string;
  userEmail: string;
  action: string;
  entityName: string;
  timestamp: string;
  ipAddress?: string;
}

export default function AuditPage() {
  const [filter, setFilter] = useState("");
  const [items, setItems] = useState<AuditEntry[]>([]);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const handle = setTimeout(() => {
      setError(null);
      api.get<Paged<AuditEntry>>("/auditlog", {
        params: { pathContains: filter || undefined, page: 1, pageSize: 100 },
      })
        .then((r) => setItems(r.data.items))
        .catch(() => setError("No fue posible cargar la auditoría (requiere ManageProjects)."));
    }, 250);
    return () => clearTimeout(handle);
  }, [filter]);

  return (
    <Box className="flex flex-col gap-4 p-4">
      <Box className="flex flex-wrap items-center justify-between gap-3">
        <Typography variant="h5" fontWeight={700}>Auditoría</Typography>
        <TextField
          size="small"
          label="Filtrar por ruta / entidad"
          value={filter}
          onChange={(e) => setFilter(e.target.value)}
          className="min-w-72"
          placeholder="ej. qualitygates, testcases"
        />
      </Box>

      {error && <Alert severity="error">{error}</Alert>}

      <Typography variant="body2" color="text.secondary">
        Traza de cambios HTTP (ISO 27001). Útil para compliance y forense de cambios en gates, casos y defectos.
      </Typography>

      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Fecha</TableCell>
              <TableCell>Usuario</TableCell>
              <TableCell>Acción</TableCell>
              <TableCell>Entidad</TableCell>
              <TableCell>IP</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {items.map((a) => (
              <TableRow key={a.id}>
                <TableCell>{new Date(a.timestamp).toLocaleString()}</TableCell>
                <TableCell>{a.userEmail}</TableCell>
                <TableCell>{a.action}</TableCell>
                <TableCell>{a.entityName}</TableCell>
                <TableCell>{a.ipAddress ?? "—"}</TableCell>
              </TableRow>
            ))}
            {items.length === 0 && !error && (
              <TableRow>
                <TableCell colSpan={5}>
                  <Typography color="text.secondary">Sin registros para el filtro actual.</Typography>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>
    </Box>
  );
}
