import { useEffect, useState } from "react";
import {
  Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle,
  MenuItem, Paper, Table, TableBody, TableCell, TableContainer, TableHead, TableRow,
  TextField, Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { api } from "../api/client";
import ProjectSelect from "../components/ProjectSelect";
import { useAuth } from "../auth/AuthContext";

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

  const load = () => {
    setError(null);
    api.get<Approval[]>("/approvals", { params: { projectId: projectId || undefined } })
      .then((r) => setItems(r.data))
      .catch(() => setError("No fue posible cargar las aprobaciones pendientes."));
  };

  useEffect(() => { void load(); }, [projectId]);

  const decide = async (id: string, approve: boolean) => {
    setError(null); setInfo(null);
    try {
      await api.post(`/approvals/${id}/decide`, { id, approve, decisionComment: approve ? "Aprobado" : "Rechazado" });
      setInfo(approve ? "Solicitud aprobada." : "Solicitud rechazada.");
      void load();
    } catch (err: unknown) {
      setError((err as { response?: { data?: { error?: string } } }).response?.data?.error
        ?? "No fue posible decidir la solicitud.");
    }
  };

  const create = async () => {
    setError(null); setInfo(null);
    if (!projectId) {
      setError("Seleccione un proyecto.");
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
      setError((err as { response?: { data?: { error?: string } } }).response?.data?.error
        ?? "No fue posible crear la solicitud.");
    }
  };

  return (
    <Box className="flex flex-col gap-4 p-4">
      <Box className="flex flex-wrap items-center justify-between gap-3">
        <Typography variant="h5" fontWeight={700}>Aprobaciones</Typography>
        <Box className="flex gap-2 items-center">
          <ProjectSelect value={projectId} onChange={setProjectId} />
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setOpen(true)}>
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
                <TableCell sx={{ fontFamily: "monospace", fontSize: 12 }}>{a.targetEntityId.slice(0, 8)}…</TableCell>
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
          <TextField select label="Tipo" value={form.type}
            onChange={(e) => setForm({ ...form, type: Number(e.target.value) })}>
            {Object.entries(TYPE_LABEL).map(([k, v]) => (
              <MenuItem key={k} value={Number(k)}>{v}</MenuItem>
            ))}
          </TextField>
          <TextField label="Id de entidad (caso / versión / gate)" value={form.targetEntityId}
            onChange={(e) => setForm({ ...form, targetEntityId: e.target.value })} required />
          <TextField label="Título" value={form.title}
            onChange={(e) => setForm({ ...form, title: e.target.value })} required />
          <TextField label="Comentario" value={form.comment} multiline minRows={2}
            onChange={(e) => setForm({ ...form, comment: e.target.value })} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setOpen(false)}>Cancelar</Button>
          <Button variant="contained" onClick={() => void create()}>Enviar</Button>
        </DialogActions>
      </Dialog>
    </Box>
  );
}
