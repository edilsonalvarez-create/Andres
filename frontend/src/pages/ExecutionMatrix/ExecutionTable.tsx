import {
  Box,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Paper,
  Chip,
  Button,
  Avatar,
  Stack,
  Typography,
  Tooltip,
  Skeleton,
  TablePagination,
} from "@mui/material";
import AttachFileIcon from "@mui/icons-material/AttachFile";
import BugReportIcon from "@mui/icons-material/BugReport";
import React, { useMemo, useState } from "react";

interface ExecutionMatrixRow {
  caseId: string;
  module: string;
  scenario: string;
  testType: string;
  currentStatus: string;
  priority: string;
  expectedResult: string;
  durationMs: number;
  executedBy: string;
  executionDate: string;
  evidence: string;
  notes: string;
}

interface ExecutionTableProps {
  rows: ExecutionMatrixRow[];
  isLoading?: boolean;
}

const getStatusColor = (status: string): "success" | "error" | "warning" | "default" => {
  switch (status.toLowerCase()) {
    case "passed":
      return "success";
    case "failed":
      return "error";
    case "skipped":
      return "warning";
    default:
      return "default";
  }
};

const getPriorityColor = (priority: string) => {
  switch (priority.toLowerCase()) {
    case "critical":
      return { bg: "#FEE2E2", text: "#DC2626", badge: "error" as const };
    case "high":
      return { bg: "#FEF3C7", text: "#F59E0B", badge: "warning" as const };
    case "medium":
      return { bg: "#DBEAFE", text: "#2563EB", badge: "info" as const };
    case "low":
      return { bg: "#F3F4F6", text: "#6B7280", badge: "default" as const };
    default:
      return { bg: "#F3F4F6", text: "#6B7280", badge: "default" as const };
  }
};

const getTestTypeIcon = (testType: string): string => {
  switch (testType.toLowerCase()) {
    case "api":
      return "🌐";
    case "ui":
      return "🖥";
    case "database":
      return "🛢";
    case "performance":
      return "⚡";
    case "security":
      return "🔒";
    default:
      return "✓";
  }
};

const getDurationColor = (ms: number) => {
  if (ms < 100) return "#16A34A";
  if (ms < 500) return "#F59E0B";
  return "#DC2626";
};

function TableSkeleton() {
  return (
    <>
      {[...Array(5)].map((_, i) => (
        <TableRow key={i}>
          {[...Array(12)].map((_, j) => (
            <TableCell key={j}>
              <Skeleton variant="text" />
            </TableCell>
          ))}
        </TableRow>
      ))}
    </>
  );
}

export default function ExecutionTable({
  rows,
  isLoading = false,
}: ExecutionTableProps) {
  const [page, setPage] = useState(0);
  const [rowsPerPage, setRowsPerPage] = useState(10);

  const paginatedRows = useMemo(() => {
    return rows.slice(page * rowsPerPage, page * rowsPerPage + rowsPerPage);
  }, [rows, page, rowsPerPage]);

  const handleChangePage = (_event: unknown, newPage: number) => {
    setPage(newPage);
  };

  const handleChangeRowsPerPage = (
    event: React.ChangeEvent<HTMLInputElement>
  ) => {
    setRowsPerPage(parseInt(event.target.value, 10));
    setPage(0);
  };

  if (isLoading) {
    return (
      <TableContainer component={Paper} sx={{ borderRadius: 2, border: "1px solid #E5E7EB" }}>
        <Table size="small">
          <TableHead>
            <TableRow sx={{ background: "#F3F4F6" }}>
              {[
                "ID Caso",
                "Módulo",
                "Escenario",
                "Tipo",
                "Estado",
                "Prioridad",
                "Resultado Esperado",
                "Duración",
                "Ejecutado por",
                "Fecha",
                "Evidencia",
                "Notas",
              ].map((header) => (
                <TableCell key={header} sx={{ fontWeight: 700, fontSize: 12 }}>
                  {header}
                </TableCell>
              ))}
            </TableRow>
          </TableHead>
          <TableBody>
            <TableSkeleton />
          </TableBody>
        </Table>
      </TableContainer>
    );
  }

  return (
    <Box>
      <TableContainer
        component={Paper}
        sx={{
          borderRadius: 2,
          border: "1px solid #E5E7EB",
          background: "linear-gradient(135deg, #FFFFFF 0%, #F8FAFC 100%)",
        }}
      >
        <Table size="small" stickyHeader>
          <TableHead>
            <TableRow
              sx={{
                background: "#F3F4F6",
                borderBottom: "2px solid #E5E7EB",
                "& th": {
                  fontWeight: 700,
                  fontSize: 12,
                  color: "#6B7280",
                  textTransform: "uppercase",
                  letterSpacing: 0.5,
                  padding: "12px 14px",
                  backgroundColor: "#F3F4F6",
                },
              }}
            >
              <TableCell sx={{ width: "140px" }}>ID Caso</TableCell>
              <TableCell sx={{ width: "100px" }}>Módulo</TableCell>
              <TableCell sx={{ width: "200px" }}>Escenario</TableCell>
              <TableCell sx={{ width: "80px" }} align="center">
                Tipo
              </TableCell>
              <TableCell sx={{ width: "100px" }} align="center">
                Estado
              </TableCell>
              <TableCell sx={{ width: "100px" }} align="center">
                Prioridad
              </TableCell>
              <TableCell sx={{ width: "150px" }}>Resultado Esperado</TableCell>
              <TableCell sx={{ width: "90px" }} align="center">
                Duración
              </TableCell>
              <TableCell sx={{ width: "120px" }} align="center">
                Ejecutado por
              </TableCell>
              <TableCell sx={{ width: "140px" }} align="center">
                Fecha
              </TableCell>
              <TableCell sx={{ width: "100px" }} align="center">
                Evidencia
              </TableCell>
              <TableCell sx={{ width: "100px" }}>Notas</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {paginatedRows.map((row, index) => (
              <TableRow
                key={row.caseId}
                sx={{
                  background: index % 2 === 0 ? "#FFFFFF" : "#F9FAFB",
                  borderBottom: "1px solid #E5E7EB",
                  transition: "all 150ms ease",
                  "&:hover": {
                    background: "#F0F9FF",
                    boxShadow: "inset 0 0 0 1px rgba(37, 99, 235, 0.1)",
                  },
                  "& td": {
                    padding: "10px 14px",
                    fontSize: 12,
                    color: "#1F2937",
                  },
                }}
              >
                {/* ID Caso */}
                <TableCell>
                  <Tooltip title="Copiar ID">
                    <Typography
                      sx={{
                        fontFamily: "monospace",
                        fontSize: 11,
                        color: "#2563EB",
                        cursor: "pointer",
                        userSelect: "none",
                        fontWeight: 600,
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                        whiteSpace: "nowrap",
                        "&:hover": {
                          textDecoration: "underline",
                        },
                      }}
                      onClick={() => navigator.clipboard.writeText(row.caseId)}
                    >
                      {row.caseId.substring(0, 12)}...
                    </Typography>
                  </Tooltip>
                </TableCell>

                {/* Módulo */}
                <TableCell>
                  <Typography sx={{ fontSize: 12, fontWeight: 500 }}>
                    {row.module || "—"}
                  </Typography>
                </TableCell>

                {/* Escenario */}
                <TableCell>
                  <Tooltip title={row.scenario}>
                    <Typography
                      sx={{
                        fontSize: 12,
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                        whiteSpace: "nowrap",
                      }}
                    >
                      {row.scenario}
                    </Typography>
                  </Tooltip>
                </TableCell>

                {/* Tipo */}
                <TableCell align="center">
                  <Tooltip title={row.testType}>
                    <Typography sx={{ fontSize: 16 }}>
                      {getTestTypeIcon(row.testType)}
                    </Typography>
                  </Tooltip>
                </TableCell>

                {/* Estado */}
                <TableCell align="center">
                  <Chip
                    label={row.currentStatus}
                    size="small"
                    color={getStatusColor(row.currentStatus)}
                    variant="filled"
                    sx={{
                      fontWeight: 600,
                      fontSize: 11,
                      height: 24,
                    }}
                  />
                </TableCell>

                {/* Prioridad */}
                <TableCell align="center">
                  <Chip
                    label={row.priority}
                    size="small"
                    color={getPriorityColor(row.priority).badge}
                    variant="filled"
                    sx={{
                      fontWeight: 600,
                      fontSize: 11,
                      height: 24,
                    }}
                  />
                </TableCell>

                {/* Resultado Esperado */}
                <TableCell>
                  <Tooltip title={row.expectedResult || "—"}>
                    <Typography
                      sx={{
                        fontSize: 12,
                        overflow: "hidden",
                        textOverflow: "ellipsis",
                        whiteSpace: "nowrap",
                        color: "#6B7280",
                      }}
                    >
                      {row.expectedResult || "—"}
                    </Typography>
                  </Tooltip>
                </TableCell>

                {/* Duración */}
                <TableCell align="center">
                  <Box sx={{ display: "flex", alignItems: "center", gap: 0.5 }}>
                    <Box
                      sx={{
                        width: 30,
                        height: 4,
                        background: "#E5E7EB",
                        borderRadius: 1,
                        overflow: "hidden",
                      }}
                    >
                      <Box
                        sx={{
                          height: "100%",
                          background: getDurationColor(row.durationMs),
                          width: `${Math.min((row.durationMs / 1000) * 100, 100)}%`,
                          transition: "width 300ms ease",
                        }}
                      />
                    </Box>
                    <Typography sx={{ fontSize: 11, fontWeight: 600, minWidth: 30 }}>
                      {row.durationMs}ms
                    </Typography>
                  </Box>
                </TableCell>

                {/* Ejecutado por */}
                <TableCell align="center">
                  <Tooltip title={row.executedBy}>
                    <Avatar
                      sx={{
                        width: 28,
                        height: 28,
                        fontSize: 11,
                        background: "#2563EB",
                        margin: "0 auto",
                      }}
                    >
                      {row.executedBy.charAt(0).toUpperCase()}
                    </Avatar>
                  </Tooltip>
                </TableCell>

                {/* Fecha */}
                <TableCell align="center">
                  <Tooltip
                    title={new Date(row.executionDate).toLocaleString()}
                  >
                    <Typography sx={{ fontSize: 11, whiteSpace: "nowrap" }}>
                      {new Date(row.executionDate).toLocaleDateString()}
                    </Typography>
                  </Tooltip>
                </TableCell>

                {/* Evidencia */}
                <TableCell align="center">
                  <Tooltip title={row.evidence || "Sin evidencia"}>
                    <Button
                      size="small"
                      variant="text"
                      startIcon={<AttachFileIcon sx={{ fontSize: 14 }} />}
                      sx={{
                        textTransform: "none",
                        fontSize: 11,
                        color: row.evidence ? "#2563EB" : "#9CA3AF",
                      }}
                      disabled={!row.evidence}
                    >
                      Ver
                    </Button>
                  </Tooltip>
                </TableCell>

                {/* Notas */}
                <TableCell>
                  {row.notes ? (
                    <Tooltip title={row.notes}>
                      <Stack direction="row" spacing={0.5} alignItems="center">
                        <BugReportIcon
                          sx={{
                            fontSize: 14,
                            color: "#DC2626",
                          }}
                        />
                        <Typography sx={{ fontSize: 11, color: "#6B7280" }}>
                          Bugs
                        </Typography>
                      </Stack>
                    </Tooltip>
                  ) : (
                    <Typography sx={{ fontSize: 11, color: "#9CA3AF" }}>—</Typography>
                  )}
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>

      {/* Paginación */}
      {rows.length > 0 && (
        <Box
          sx={{
            mt: 2,
            display: "flex",
            justifyContent: "space-between",
            alignItems: "center",
            px: 2,
            py: 1,
          }}
        >
          <Typography sx={{ fontSize: 12, color: "#6B7280" }}>
            {rows.length > 0
              ? `${page * rowsPerPage + 1} - ${Math.min(
                  (page + 1) * rowsPerPage,
                  rows.length
                )} de ${rows.length}`
              : "0 resultados"}
          </Typography>
          <TablePagination
            rowsPerPageOptions={[5, 10, 25, 50]}
            component="div"
            count={rows.length}
            rowsPerPage={rowsPerPage}
            page={page}
            onPageChange={handleChangePage}
            onRowsPerPageChange={handleChangeRowsPerPage}
            sx={{
              "& .MuiTablePagination-selectLabel, .MuiTablePagination-displayedRows":
                {
                  margin: 0,
                  fontSize: 12,
                },
            }}
          />
        </Box>
      )}

      {rows.length === 0 && (
        <Box sx={{ textAlign: "center", py: 6, color: "#6B7280" }}>
          <Typography variant="body2">No se encontraron resultados</Typography>
        </Box>
      )}
    </Box>
  );
}
