import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import {
  Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle,
  Divider, MenuItem, Paper, Snackbar, Stack, Table, TableBody, TableCell, TableContainer,
  TableHead, TablePagination, TableRow, TextField, Typography,
} from "@mui/material";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import DownloadIcon from "@mui/icons-material/Download";
import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import TableChartIcon from "@mui/icons-material/TableChart";
import * as signalR from "@microsoft/signalr";
import { api, getStoredAuth } from "../api/client";
import ProjectSelect from "../components/ProjectSelect";
import AiDiagnosisPanel from "../components/AiDiagnosisPanel";
import { ENVIRONMENTS, RUN_STATUS, TEST_TYPES, type Paged, type TestRun } from "../types";
import ExecutionKPICards from "./ExecutionMatrix/ExecutionKPICards";
import ExecutionFilters from "./ExecutionMatrix/ExecutionFilters";
import ExecutionToolbar from "./ExecutionMatrix/ExecutionToolbar";
import ExecutionTable from "./ExecutionMatrix/ExecutionTable";

// Detalle de una corrida (matriz de ejecución).
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

interface ExecutionMatrix {
  testRunId: string;
  totalTests: number;
  passedTests: number;
  failedTests: number;
  skippedTests: number;
  passRatePercent: number;
  rows: ExecutionMatrixRow[];
  uniqueModules: string[];
  uniquePriorities: string[];
  uniqueStatuses: string[];
}

const fmtDate = (d?: string) => (d ? new Date(d).toLocaleString() : "—");

export default function TestRunsPage() {
  const [projectId, setProjectId] = useState("");
  const [data, setData] = useState<Paged<TestRun> | null>(null);
  const [page, setPage] = useState(0);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [form, setForm] = useState({ runType: 8, environment: 2 });
  const [toast, setToast] = useState<string | null>(null);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  // Vista interna de detalle (matriz). Si hay corrida seleccionada → vista Matriz.
  const [selectedRun, setSelectedRun] = useState<TestRun | null>(null);
  const [matrix, setMatrix] = useState<ExecutionMatrix | null>(null);
  const [matrixLoading, setMatrixLoading] = useState(false);

  // Filtros del detalle.
  const [searchValue, setSearchValue] = useState("");
  const [moduleFilter, setModuleFilter] = useState("");
  const [priorityFilter, setPriorityFilter] = useState("");
  const [statusFilter, setStatusFilter] = useState("");

  const load = useCallback(() => {
    if (!projectId) return;
    api
      .get<Paged<TestRun>>("/testruns", { params: { projectId, page: page + 1, pageSize: 10 } })
      .then((r) => setData(r.data));
  }, [projectId, page]);

  useEffect(() => { void load(); }, [load]);

  // Al cambiar de proyecto, volver a la lista y limpiar el detalle.
  const handleProjectChange = (id: string) => {
    setProjectId(id);
    setPage(0);
    setSelectedRun(null);
    setMatrix(null);
  };

  // Suscripción en tiempo real al progreso de las ejecuciones visibles.
  // Auth: accessTokenFactory → Authorization (negotiate/LongPolling). Sin ?access_token= en la URL.
  // Producción: LongPolling (el handshake WebSocket del navegador no admite Authorization header).
  useEffect(() => {
    const token = getStoredAuth()?.accessToken;
    if (!token || !data?.items.length) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl("/hubs/testruns", {
        accessTokenFactory: () => getStoredAuth()?.accessToken ?? "",
        ...(import.meta.env.PROD
          ? { transport: signalR.HttpTransportType.LongPolling }
          : {}),
      })
      .withAutomaticReconnect()
      .build();
    connectionRef.current = connection;

    connection.on("runCompleted", (payload: { testRunId: string; gateStatus: string }) => {
      setToast(`Ejecución finalizada — Quality Gate: ${payload.gateStatus}`);
      void load();
    });
    connection.on("runStatusChanged", () => void load());

    connection
      .start()
      .then(() =>
        Promise.all(
          data.items
            .filter((r) => r.status === 1 || r.status === 2)
            .map((r) => connection.invoke("SubscribeToRun", r.id))
        )
      )
      .catch(() => undefined);

    return () => { void connection.stop(); };
  }, [data, load]);

  const startRun = async () => {
    await api.post("/testruns", { projectId, ...form });
    setDialogOpen(false);
    setToast("Ejecución encolada; se procesará en segundo plano.");
    void load();
  };

  const downloadReport = (runId: string, format: string) => {
    void api
      .get(`/testruns/${runId}/report`, { params: { format }, responseType: "blob" })
      .then((r) => {
        const url = URL.createObjectURL(r.data as Blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = `reporte-${runId}.${format.toLowerCase() === "excel" ? "xlsx" : format.toLowerCase()}`;
        link.click();
        URL.revokeObjectURL(url);
      });
  };

  // Abrir la vista interna de Matriz para una corrida (drill-down).
  const openMatrix = useCallback(async (run: TestRun) => {
    setSelectedRun(run);
    setMatrixLoading(true);
    setMatrix(null);
    setSearchValue("");
    setModuleFilter("");
    setPriorityFilter("");
    setStatusFilter("");
    try {
      const { data: m } = await api.get<ExecutionMatrix>(`/testruns/${run.id}/execution-matrix/json`);
      setMatrix(m);
    } catch {
      setToast("No se pudo cargar el detalle de la ejecución.");
    } finally {
      setMatrixLoading(false);
    }
  }, []);

  const backToList = () => {
    setSelectedRun(null);
    setMatrix(null);
  };

  const downloadMatrixExcel = () => {
    if (!selectedRun) return;
    void api
      .get(`/testruns/${selectedRun.id}/execution-matrix`, { responseType: "blob" })
      .then((r) => {
        const url = URL.createObjectURL(r.data as Blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = `matriz-ejecucion-${selectedRun.id}.xlsx`;
        link.click();
        URL.revokeObjectURL(url);
      });
  };

  const filteredRows = useMemo(() => {
    if (!matrix) return [];
    return matrix.rows.filter((row) => {
      if (moduleFilter && row.module !== moduleFilter) return false;
      if (priorityFilter && row.priority !== priorityFilter) return false;
      if (statusFilter && row.currentStatus !== statusFilter) return false;
      if (searchValue.trim()) {
        const q = searchValue.toLowerCase();
        return (
          row.caseId.toLowerCase().includes(q) ||
          row.scenario.toLowerCase().includes(q) ||
          row.executedBy.toLowerCase().includes(q) ||
          row.module.toLowerCase().includes(q) ||
          row.evidence.toLowerCase().includes(q) ||
          row.notes.toLowerCase().includes(q) ||
          row.expectedResult.toLowerCase().includes(q)
        );
      }
      return true;
    });
  }, [matrix, moduleFilter, priorityFilter, statusFilter, searchValue]);

  const hasActiveFilters =
    moduleFilter !== "" || priorityFilter !== "" || statusFilter !== "" || searchValue !== "";

  const clearFilters = () => {
    setModuleFilter("");
    setPriorityFilter("");
    setStatusFilter("");
    setSearchValue("");
  };

  const statusColor = (status: number) =>
    status === 3 ? "success" : status === 4 ? "error" : status === 2 ? "info" : "default";

  const gateColor = (gate?: string) =>
    gate === "Passed" ? "success" : gate === "Failed" ? "error" : "warning";

  // ─────────────────────────────────────────────────────────────
  // VISTA INTERNA: Matriz de ejecución de la corrida seleccionada.
  // ─────────────────────────────────────────────────────────────
  if (selectedRun) {
    return (
      <div className="flex flex-col gap-4">
        {/* Encabezado de la vista interna */}
        <Box className="flex items-center justify-between">
          <Box className="flex items-center gap-3">
            <Button variant="text" startIcon={<ArrowBackIcon />} onClick={backToList}>
              Volver a ejecuciones
            </Button>
            <Divider orientation="vertical" flexItem />
            <Box className="flex items-center gap-2">
              <TableChartIcon color="primary" />
              <Box>
                <Typography variant="h6" fontWeight={700} lineHeight={1.2}>
                  Matriz de ejecución
                </Typography>
                <Typography variant="body2" color="text.secondary">
                  {TEST_TYPES[selectedRun.runType]} · {ENVIRONMENTS[selectedRun.environment]}
                </Typography>
              </Box>
            </Box>
          </Box>
          <Button
            variant="contained"
            color="success"
            startIcon={<DownloadIcon />}
            onClick={downloadMatrixExcel}
            disabled={!matrix}
          >
            Descargar Excel
          </Button>
        </Box>

        {/* Metadatos de la corrida */}
        <Paper variant="outlined" sx={{ p: 2 }}>
          <Stack
            direction={{ xs: "column", md: "row" }}
            spacing={3}
            divider={<Divider orientation="vertical" flexItem />}
          >
            <Meta label="Estado">
              <Chip label={RUN_STATUS[selectedRun.status]} size="small" color={statusColor(selectedRun.status)} />
            </Meta>
            <Meta label="Quality Gate">
              {selectedRun.gateStatus ? (
                <Chip label={selectedRun.gateStatus} size="small" color={gateColor(selectedRun.gateStatus)} />
              ) : (
                <Typography variant="body2">—</Typography>
              )}
            </Meta>
            <Meta label="Disparada por">
              <Typography variant="body2">{selectedRun.triggeredBy}</Typography>
            </Meta>
            <Meta label="Inicio">
              <Typography variant="body2">{fmtDate(selectedRun.startedAt)}</Typography>
            </Meta>
            <Meta label="Fin">
              <Typography variant="body2">{fmtDate(selectedRun.completedAt)}</Typography>
            </Meta>
          </Stack>
        </Paper>

        {/* KPIs */}
        <ExecutionKPICards
          totalTests={matrix?.totalTests ?? selectedRun.totalTests}
          passedTests={matrix?.passedTests ?? selectedRun.passed}
          failedTests={matrix?.failedTests ?? selectedRun.failed}
          passRatePercent={matrix?.passRatePercent ?? selectedRun.passRatePercent}
          isLoading={matrixLoading}
        />

        <AiDiagnosisPanel testRunId={selectedRun.id} />

        {/* Filtros, búsqueda y tabla detallada */}
        {matrix && (
          <Box>
            <ExecutionFilters
              modules={matrix.uniqueModules}
              priorities={matrix.uniquePriorities}
              statuses={matrix.uniqueStatuses}
              moduleFilter={moduleFilter}
              priorityFilter={priorityFilter}
              statusFilter={statusFilter}
              onModuleChange={(v) => setModuleFilter(v ?? "")}
              onPriorityChange={(v) => setPriorityFilter(v ?? "")}
              onStatusChange={(v) => setStatusFilter(v ?? "")}
            />
            <ExecutionToolbar
              searchValue={searchValue}
              onSearchChange={setSearchValue}
              onClearFilters={clearFilters}
              hasActiveFilters={hasActiveFilters}
            />
            <ExecutionTable rows={filteredRows} isLoading={matrixLoading} />
          </Box>
        )}

        <Snackbar
          open={toast !== null}
          autoHideDuration={5000}
          onClose={() => setToast(null)}
          message={toast}
        />
      </div>
    );
  }

  // ─────────────────────────────────────────────────────────────
  // VISTA PRINCIPAL: lista de ejecuciones.
  // ─────────────────────────────────────────────────────────────
  return (
    <div className="flex flex-col gap-4">
      <Box className="flex items-center justify-between">
        <Typography variant="h5" fontWeight={700}>
          Ejecuciones de prueba
        </Typography>
        <Box className="flex gap-3">
          <ProjectSelect value={projectId} onChange={handleProjectChange} />
          <Button variant="contained" startIcon={<PlayArrowIcon />} onClick={() => setDialogOpen(true)}
            disabled={!projectId}>
            Nueva ejecución
          </Button>
        </Box>
      </Box>

      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Tipo</TableCell>
              <TableCell>Ambiente</TableCell>
              <TableCell>Estado</TableCell>
              <TableCell align="center">Total</TableCell>
              <TableCell align="center">✅</TableCell>
              <TableCell align="center">❌</TableCell>
              <TableCell align="center">% Éxito</TableCell>
              <TableCell>Quality Gate</TableCell>
              <TableCell>Disparada por</TableCell>
              <TableCell align="center">Detalle</TableCell>
              <TableCell align="center">Reporte</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {data?.items.map((run) => (
              <TableRow
                key={run.id}
                hover
                onClick={() => void openMatrix(run)}
                sx={{ cursor: "pointer" }}
              >
                <TableCell>{TEST_TYPES[run.runType]}</TableCell>
                <TableCell>{ENVIRONMENTS[run.environment]}</TableCell>
                <TableCell>
                  <Chip label={RUN_STATUS[run.status]} size="small" color={statusColor(run.status)} />
                </TableCell>
                <TableCell align="center">{run.totalTests}</TableCell>
                <TableCell align="center">{run.passed}</TableCell>
                <TableCell align="center">{run.failed}</TableCell>
                <TableCell align="center">{run.passRatePercent}%</TableCell>
                <TableCell>
                  {run.gateStatus ? (
                    <Chip label={run.gateStatus} size="small" color={gateColor(run.gateStatus)} />
                  ) : "—"}
                </TableCell>
                <TableCell>{run.triggeredBy}</TableCell>
                <TableCell align="center">
                  <Button size="small" startIcon={<TableChartIcon />}
                    onClick={(e) => { e.stopPropagation(); void openMatrix(run); }}>
                    Ver matriz
                  </Button>
                </TableCell>
                <TableCell align="center">
                  <Button size="small" startIcon={<DownloadIcon />}
                    onClick={(e) => { e.stopPropagation(); downloadReport(run.id, "Pdf"); }}
                    disabled={run.status !== 3}>
                    PDF
                  </Button>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        <TablePagination
          component="div"
          count={data?.totalCount ?? 0}
          page={page}
          onPageChange={(_, p) => setPage(p)}
          rowsPerPage={10}
          rowsPerPageOptions={[10]}
        />
      </TableContainer>

      <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Nueva ejecución</DialogTitle>
        <DialogContent className="flex flex-col gap-4 pt-2">
          <TextField select label="Tipo de prueba" value={form.runType}
            onChange={(e) => setForm({ ...form, runType: Number(e.target.value) })}>
            {Object.entries(TEST_TYPES).slice(0, 8).map(([value, label]) => (
              <MenuItem key={value} value={Number(value)}>{label}</MenuItem>
            ))}
          </TextField>
          <TextField select label="Ambiente" value={form.environment}
            onChange={(e) => setForm({ ...form, environment: Number(e.target.value) })}>
            {Object.entries(ENVIRONMENTS).map(([value, label]) => (
              <MenuItem key={value} value={Number(value)}>{label}</MenuItem>
            ))}
          </TextField>
          <Alert severity="info">
            La ejecución corre en segundo plano; el estado se actualiza en tiempo real.
          </Alert>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDialogOpen(false)}>Cancelar</Button>
          <Button variant="contained" onClick={() => void startRun()}>Ejecutar</Button>
        </DialogActions>
      </Dialog>

      <Snackbar
        open={toast !== null}
        autoHideDuration={5000}
        onClose={() => setToast(null)}
        message={toast}
      />
    </div>
  );
}

/** Celda de metadato etiqueta/valor para el encabezado de la matriz. */
function Meta({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <Box>
      <Typography variant="caption" color="text.secondary" sx={{ display: "block", fontWeight: 600 }}>
        {label}
      </Typography>
      <Box sx={{ mt: 0.5 }}>{children}</Box>
    </Box>
  );
}
