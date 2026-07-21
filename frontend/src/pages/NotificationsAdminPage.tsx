import { useEffect, useState, type FormEvent } from "react";
import {
  Alert, Box, Button, Checkbox, Dialog, DialogActions, DialogContent, DialogTitle,
  FormControlLabel, FormGroup, MenuItem, Paper, Switch, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, TextField, Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { api, extractApiErrorMessage } from "../api/client";
import ProjectSelect from "../components/ProjectSelect";

const CHANNELS = [
  { value: 1, label: "Email" },
  { value: 2, label: "Microsoft Teams" },
  { value: 3, label: "Slack" },
  { value: 4, label: "Discord" },
  { value: 5, label: "Telegram" },
];

const EVENT_FLAGS = [
  { bit: 1, label: "Prueba fallida" },
  { bit: 2, label: "Vulnerabilidad" },
  { bit: 4, label: "Deploy rechazado" },
  { bit: 8, label: "Ejecución completada" },
  { bit: 16, label: "Defecto creado" },
];

interface Channel {
  id: string;
  projectId?: string | null;
  channel: number;
  targetHint?: string | null;
  isConfigured: boolean;
  events: number;
  isEnabled: boolean;
}

export default function NotificationsAdminPage() {
  const [projectId, setProjectId] = useState("");
  const [items, setItems] = useState<Channel[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [info, setInfo] = useState<string | null>(null);
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({
    channel: 3,
    target: "",
    events: 31,
    isEnabled: true,
  });

  const load = () => {
    setError(null);
    api.get<Channel[]>("/admin/notification-channels", {
      params: { projectId: projectId || undefined },
    })
      .then((r) => setItems(r.data))
      .catch(() => setError("No fue posible cargar los canales (permiso ManageProjects)."));
  };

  useEffect(() => { void load(); }, [projectId]);

  const toggleEvent = (bit: number) => {
    setForm((f) => ({
      ...f,
      events: (f.events & bit) === bit ? f.events & ~bit : f.events | bit,
    }));
  };

  const save = async (e: FormEvent) => {
    e.preventDefault();
    setError(null); setInfo(null);
    try {
      await api.post("/admin/notification-channels", {
        projectId: projectId || null,
        channel: form.channel,
        target: form.target,
        events: form.events,
        isEnabled: form.isEnabled,
      });
      setOpen(false);
      setInfo("Canal guardado.");
      void load();
    } catch (err: unknown) {
      setError(extractApiErrorMessage(err) ?? "No fue posible guardar el canal.");
    }
  };

  return (
    <Box className="flex flex-col gap-4 p-4">
      <Box className="flex flex-wrap items-center justify-between gap-3">
        <Typography variant="h5" fontWeight={700}>Notificaciones</Typography>
        <Box className="flex gap-2 items-center">
          <ProjectSelect value={projectId} onChange={setProjectId} />
          <Button variant="contained" startIcon={<AddIcon />} onClick={() => setOpen(true)}>
            Canal
          </Button>
        </Box>
      </Box>

      {error && <Alert severity="error">{error}</Alert>}
      {info && <Alert severity="success" onClose={() => setInfo(null)}>{info}</Alert>}

      <Typography variant="body2" color="text.secondary">
        Configure Email, Teams, Slack, Discord o Telegram. Los eventos se disparan al fallar pruebas,
        completar runs, crear defectos o rechazar quality gates.
      </Typography>

      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Canal</TableCell>
              <TableCell>Destino</TableCell>
              <TableCell>Eventos</TableCell>
              <TableCell>Activo</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {items.map((c) => (
              <TableRow key={c.id}>
                <TableCell>{CHANNELS.find((x) => x.value === c.channel)?.label ?? c.channel}</TableCell>
                <TableCell sx={{ maxWidth: 320, overflow: "hidden", textOverflow: "ellipsis" }}>
                  {c.isConfigured
                    ? (c.targetHint ?? "Configurado")
                    : "—"}
                </TableCell>
                <TableCell>
                  {EVENT_FLAGS.filter((f) => (c.events & f.bit) === f.bit).map((f) => f.label).join(", ") || "—"}
                </TableCell>
                <TableCell>{c.isEnabled ? "Sí" : "No"}</TableCell>
              </TableRow>
            ))}
            {items.length === 0 && (
              <TableRow>
                <TableCell colSpan={4}>
                  <Typography color="text.secondary">Sin canales configurados.</Typography>
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </TableContainer>

      <Dialog open={open} onClose={() => setOpen(false)} fullWidth maxWidth="sm">
        <form onSubmit={save}>
          <DialogTitle>Nuevo / actualizar canal</DialogTitle>
          <DialogContent className="flex flex-col gap-3 pt-2">
            <TextField
              select label="Canal" value={form.channel}
              onChange={(e) => setForm({ ...form, channel: Number(e.target.value) })}
            >
              {CHANNELS.map((c) => (
                <MenuItem key={c.value} value={c.value}>{c.label}</MenuItem>
              ))}
            </TextField>
            <TextField
              label={form.channel === 1 ? "Correo" : "Webhook / destino"}
              value={form.target}
              onChange={(e) => setForm({ ...form, target: e.target.value })}
              required
              helperText={form.channel === 1 ? "usuario@empresa.com" : "URL HTTPS del webhook"}
            />
            <FormGroup>
              {EVENT_FLAGS.map((f) => (
                <FormControlLabel
                  key={f.bit}
                  control={
                    <Checkbox
                      checked={(form.events & f.bit) === f.bit}
                      onChange={() => toggleEvent(f.bit)}
                    />
                  }
                  label={f.label}
                />
              ))}
            </FormGroup>
            <FormControlLabel
              control={
                <Switch
                  checked={form.isEnabled}
                  onChange={(e) => setForm({ ...form, isEnabled: e.target.checked })}
                />
              }
              label="Activo"
            />
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setOpen(false)}>Cancelar</Button>
            <Button type="submit" variant="contained">Guardar</Button>
          </DialogActions>
        </form>
      </Dialog>
    </Box>
  );
}
