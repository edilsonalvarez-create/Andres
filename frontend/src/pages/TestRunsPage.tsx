import { useCallback, useEffect, useRef, useState } from "react";
import {
  Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle,
  MenuItem, Paper, Snackbar, Table, TableBody, TableCell, TableContainer,
  TableHead, TablePagination, TableRow, TextField, Typography,
} from "@mui/material";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import DownloadIcon from "@mui/icons-material/Download";
import * as signalR from "@microsoft/signalr";
import { api, getStoredAuth } from "../api/client";
import ProjectSelect from "../components/ProjectSelect";
import { ENVIRONMENTS, RUN_STATUS, TEST_TYPES, type Paged, type TestRun } from "../types";

export default function TestRunsPage() {
  const [projectId, setProjectId] = useState("");
  const [data, setData] = useState<Paged<TestRun> | null>(null);
  const [page, setPage] = useState(0);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [form, setForm] = useState({ runType: 8, environment: 2 });
  const [toast, setToast] = useState<string | null>(null);
  const connectionRef = useRef<signalR.HubConnection | null>(null);

  const load = useCallback(() => {
    if (!projectId) return;
    api
      .get<Paged<TestRun>>("/testruns", { params: { projectId, page: page + 1, pageSize: 10 } })
      .then((r) => setData(r.data));
  }, [projectId, page]);

  useEffect(() => { void load(); }, [load]);

  // Suscripción en tiempo real al progreso de las ejecuciones visibles.
  useEffect(() => {
    const token = getStoredAuth()?.accessToken;
    if (!token || !data?.items.length) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`/hubs/testruns?access_token=${token}`)
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

  const statusColor = (status: number) =>
    status === 3 ? "success" : status === 4 ? "error" : status === 2 ? "info" : "default";

  return (
    <div className="flex flex-col gap-4">
      <Box className="flex items-center justify-between">
        <Typography variant="h5" fontWeight={700}>
          Ejecuciones de prueba
        </Typography>
        <Box className="flex gap-3">
          <ProjectSelect value={projectId} onChange={(id) => { setProjectId(id); setPage(0); }} />
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
              <TableCell align="center">Reporte</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {data?.items.map((run) => (
              <TableRow key={run.id} hover>
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
                    <Chip
                      label={run.gateStatus}
                      size="small"
                      color={run.gateStatus === "Passed" ? "success" : run.gateStatus === "Failed" ? "error" : "warning"}
                    />
                  ) : "—"}
                </TableCell>
                <TableCell>{run.triggeredBy}</TableCell>
                <TableCell align="center">
                  <Button size="small" startIcon={<DownloadIcon />}
                    onClick={() => downloadReport(run.id, "Pdf")}
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
