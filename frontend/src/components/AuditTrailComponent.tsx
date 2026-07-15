import { useEffect, useState } from "react";
import {
  Alert,
  Box,
  Card,
  CardContent,
  CircularProgress,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TablePagination,
  TableRow,
  Typography,
} from "@mui/material";
import HistoryIcon from "@mui/icons-material/History";
import { api } from "../api/client";

interface AuditLogEntry {
  id: string;
  userEmail: string;
  action: string;
  entityName: string;
  timestamp: string;
  ipAddress?: string;
}

interface AuditLogPagedResult {
  items: AuditLogEntry[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

interface Props {
  /** Filtra por coincidencia parcial en la ruta HTTP auditada (ej: un Guid de proyecto/entidad). */
  pathContains?: string;
  maxRows?: number;
}

const ACTION_BACKGROUND: Record<string, string> = {
  Created: "rgba(76, 175, 80, 0.2)",
  Updated: "rgba(33, 150, 243, 0.2)",
  Deleted: "rgba(244, 67, 54, 0.2)",
};

export default function AuditTrailComponent({ pathContains, maxRows = 10 }: Props) {
  const [logs, setLogs] = useState<AuditLogPagedResult | null>(null);
  const [loading, setLoading] = useState(false);
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(maxRows);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    api
      .get<AuditLogPagedResult>("/audit-log", {
        params: { pathContains, page: page + 1, pageSize },
      })
      .then((r) => {
        if (!cancelled) setLogs(r.data);
      })
      .catch((error) => console.error("Error loading audit logs:", error))
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [page, pageSize, pathContains]);

  const formatDate = (dateString: string) =>
    new Date(dateString).toLocaleString("es-ES", {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    });

  if (!logs && loading) {
    return <CircularProgress />;
  }

  return (
    <Card>
      <CardContent>
        <Box className="flex items-center gap-2 mb-4">
          <HistoryIcon />
          <Typography variant="h6" fontWeight={700}>
            Historial de Auditoría
          </Typography>
        </Box>

        {!logs || logs.items.length === 0 ? (
          <Alert severity="info">No hay registros de auditoría.</Alert>
        ) : (
          <>
            <TableContainer component={Paper} variant="outlined">
              <Table size="small">
                <TableHead>
                  <TableRow>
                    <TableCell><strong>Acción</strong></TableCell>
                    <TableCell><strong>Usuario</strong></TableCell>
                    <TableCell><strong>Ruta</strong></TableCell>
                    <TableCell><strong>Fecha/Hora</strong></TableCell>
                    <TableCell align="right"><strong>IP</strong></TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {logs.items.map((log) => (
                    <TableRow key={log.id} hover>
                      <TableCell>
                        <span
                          style={{
                            padding: "4px 8px",
                            borderRadius: "4px",
                            backgroundColor: ACTION_BACKGROUND[log.action] ?? "rgba(0,0,0,0.08)",
                            fontSize: "0.85rem",
                            fontWeight: 600,
                          }}
                        >
                          {log.action}
                        </span>
                      </TableCell>
                      <TableCell>{log.userEmail}</TableCell>
                      <TableCell sx={{ fontSize: "0.8rem" }}>{log.entityName}</TableCell>
                      <TableCell>{formatDate(log.timestamp)}</TableCell>
                      <TableCell align="right" sx={{ fontSize: "0.85rem" }}>
                        {log.ipAddress || "—"}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>

            {logs.totalPages > 1 && (
              <TablePagination
                rowsPerPageOptions={[5, 10, 25, 50]}
                component="div"
                count={logs.totalCount}
                rowsPerPage={pageSize}
                page={page}
                onPageChange={(_, newPage) => setPage(newPage)}
                onRowsPerPageChange={(e) => {
                  setPageSize(parseInt(e.target.value, 10));
                  setPage(0);
                }}
                labelRowsPerPage="Filas por página:"
                labelDisplayedRows={({ from, to, count }) => `${from}–${to} de ${count}`}
              />
            )}
          </>
        )}
      </CardContent>
    </Card>
  );
}
