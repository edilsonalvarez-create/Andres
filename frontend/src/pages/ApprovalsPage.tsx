import { useEffect, useMemo, useState } from "react";
import {
  Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle,
  MenuItem, Paper, Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  TextField, Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { api, extractApiErrorMessage } from "../api/client";
import ProjectSelect from "../components/ProjectSelect";
import { useAuth } from "../auth/AuthContext";
import type { Paged, ProjectVersion, TestCase } from "../types";

const TYPE_LABEL: Record<number, string> = {
  1: "Activación de caso",
  2: "Release de versión",
  3: "Override Quality Gate",
};

interface Approval {
  id: string;
  projectId: string;
  type: number;
  targetEntityId: string;
  title: string;
  comment?: string;
  status: number;
  createdAt: string;
}

interface QualityGateOption {
  id: string;
  name: string;
}

function versionLabel(v: ProjectVersion): string {
  return `v${v.number}${v.releasedAt ? " (liberada)" : ""}`;
}

export default function ApprovalsPage() {
  const { hasRole } = useAuth();
  const canDecide = hasRole("Administrador") || hasRole("LiderTecnico") || hasRole("ProductOwner");
  const [projectId, setProjectId] = useState("");
  const [items, setItems] = useState<Approval[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({
    type: 1,
    targetEntityId: "",
    title: "",
    comment: "",
  });
  const [testCases, setTestCases] = useState<TestCase[]>([]);
  const [versions, setVersions] = useState<ProjectVersion[]>([]);
  const [gates, setGates] = useState<QualityGateOption[]>([]);
  const [optionsLoading, setOptionsLoading] = useState(false);

  const entityLabelById = useMemo(() => {
    const map = new Map<string, string>();
    for (const tc of testCases) map.set(tc.id, `${tc.code} — ${tc.title}`);
    for (const v of versions) map.set(v.id, versionLabel(v));
    for (const g of gates) map.set(g.id, g.name);
    return map;
  }, [testCases, versions, gates]);

  const load = () => {
    setError(null);
    api.get<Approval[]>("/approvals", { params: { projectId: projectId || undefined } })
      .then((r) => setItems(r.data))
      .catch(() => setError("No fue posible cargar las aprobaciones pendientes."));
  };

  useEffect(() => { void load(); }, [projectId]);

  useEffect(() => {
    if (!projectId) {
      setTestCases([]);
      setVersions([]);
      return;
    }
    setOptionsLoading(true);
    Promise.all([
      api.get<Paged<TestCase>>("/testcases", { params: { projectId, page: 1, pageSize: 200 } })
        .then((r) => setTestCases(r.data.items)),
      api.get<ProjectVersion[]>(`/catalog/projects/${projectId}/versions`)
        .then((r) => setVersions(r.data)),
      api.get<QualityGateOption[]>("/qualitygates")
        .then((r) => setGates(r.data)),
    ])
      .catch(() => setError("No fue posible cargar las entidades para seleccionar."))
      .finally(() => setOptionsLoading(false));
  }, [projectId]);

  const decide = async (id: string, approve: boolean) => {
    setError(null); setInfo(null);
    try {
      await api.post(`/approvals/${id}/decide`, { id, approve, decisionComment: approve ? "Aprobado" : "Rechazado" });
      setInfo(approve ? "Solicitud aprobada." : "Solicitud rechazada.");
      void load();
    } catch (err: unknown) {
      setError(extractApiErrorMessage(err) ?? "No fue posible decidir la solicitud.");
    }
  };

  const openCreate = () => {
    setForm({ type: 1, targetEntityId: "", title: "", comment: "" });
    setOpen(true);
  };

  const create = async () => {
    setError(null); setInfo(null);
    if (!projectId) {
      setError("Seleccione un proyecto.");
      return;
    }
    if (!form.targetEntityId) {
      setError("Seleccione la entidad objetivo.");
      return;
    }
    if (!form.title.trim()) {
      setError("Indique un título.");
      return;
    }
    try {
      await api.post("/approvals", {
        projectId,
        type: form.type,
        targetEntityId: form.targetEntityId,
        title: form.title,
        comment: form.comment || null,
      });
      setOpen(false);
      setInfo("Solicitud creada.");
      void load();
    } catch (err: unknown) {
      setError(extractApiErrorMessage(err) ?? "No fue posible crear la solicitud.");
    }
  };

  const targetOptions = form.type === 1
    ? testCases.map((tc) => ({ id: tc.id, label: `${tc.code} — ${tc.title}` }))
    : form.type === 2
      ? versions.map((v) => ({ id: v.id, label: versionLabel(v) }))
      : gates.map((g) => ({ id: g.id, label: g.name }));

  const targetHelper = form.type === 1
    ? "Caso de prueba a activar"
    : form.type === 2
      ? "Versión del catálogo a liberar"
      : "Quality Gate a sobrescribir";

  return (
    <Box className="flex flex-col gap-4 p-4">
      <Box className="flex flex-wrap items-center justify-between gap-3">
        <Typography variant="h5" fontWeight={700}>Aprobaciones</Typography>
        <Box className="flex gap-2 items-center">
          <ProjectSelect value={projectId} onChange={setProjectId} />
          <Button variant="contained" startIcon={<AddIcon />}
            disabled={!projectId} onClick={openCreate}>
            Solicitar
          </Button>
        </Box>
      </Box>

      {error && <Alert severity="error">{error}</Alert>}
      {info && <Alert severity="success" onClose={() => setInfo(null)}>{info}</Alert>}

      <Typography variant="body2" color="text.secondary">
        Sign-off enterprise: activación de casos, release de versiones y override de quality gates.
        Deciden LiderTecnico, ProductOwner o Administrador.
      </Typography>

      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Tipo</TableCell>
              <TableCell>Título</TableCell>
              <TableCell>Entidad</TableCell>
              <TableCell>Creada</TableCell>
              <TableCell align="right">Acciones</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {items.map((a) => (
              <TableRow key={a.id}>
                <TableCell><Chip size="small" label={TYPE_LABEL[a.type] ?? a.type} /></TableCell>
                <TableCell>{a.title}</TableCell>
                <TableCell>
                  {entityLabelById.get(a.targetEntityId) ?? (
                    <Typography component="span" sx={{ fontFamily: "monospace", fontSize: 12 }}>
                      {a.targetEntityId.slice(0, 8)}…
                    </Typography>
                  )}
                </TableCell>
                <TableCell>{new Date(a.createdAt).toLocaleString()}</TableCell>
                <TableCell align="right">
                  {canDecide && (
                    <Box className="flex gap-1 justify-end">
                      <Button size="small" color="success" onClick={() => void decide(a.id, true)}>Aprobar</Button>
                      <Button size="small" color="error" onClick={() => void decide(a.id, false)}>Rechazar</Button>
                    </Box>
                  )}
                </TableCell>
              </TableRow>
            ))}
            {items.length === 0 && (
              <TableRow>
                <TableCell colSpan={5}>
                  <Typography color="text.secondary">No hay solicitudes pendientes.</Typography>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <Dialog open={open} onClose={() => setOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>Nueva solicitud de aprobación</DialogTitle>
        <DialogContent className="flex flex-col gap-3 pt-2">
          {!projectId && (
            <Alert severity="warning">Seleccione un proyecto antes de crear la solicitud.</Alert>
          )}
          <TextField select label="Tipo" value={form.type}
            onChange={(e) => setForm({
              ...form,
              type: Number(e.target.value),
              targetEntityId: "",
            })}>
            {Object.entries(TYPE_LABEL).map(([k, v]) => (
              <MenuItem key={k} value={Number(k)}>{v}</MenuItem>
            ))}
          </TextField>
          <TextField
            select
            required
            label="Entidad objetivo"
            value={form.targetEntityId}
            onChange={(e) => setForm({ ...form, targetEntityId: e.target.value })}
            disabled={!projectId || optionsLoading}
            helperText={optionsLoading ? "Cargando opciones…" : targetHelper}
          >
            {targetOptions.length === 0 ? (
              <MenuItem value="" disabled>
                {optionsLoading ? "Cargando…" : "No hay opciones para este tipo"}
              </MenuItem>
            ) : (
              targetOptions.map((opt) => (
                <MenuItem key={opt.id} value={opt.id}>{opt.label}</MenuItem>
              ))
            )}
          </TextField>
          <TextField label="Título" value={form.title}
            onChange={(e) => setForm({ ...form, title: e.target.value })} required />
          <TextField label="Comentario" value={form.comment} multiline minRows={2}
            onChange={(e) => setForm({ ...form, comment: e.target.value })} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpen(false)}>Cancelar</Button>
          <Button variant="contained" disabled={!projectId || !form.targetEntityId || !form.title.trim()}
            onClick={() => void create()}>
            Enviar
          </Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
