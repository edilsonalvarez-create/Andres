import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle,
  IconButton, ListItemText, Menu, MenuItem, TextField, Tooltip, Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import MoreVertIcon from "@mui/icons-material/MoreVert";
import { api } from "../api/client";
import { useAuth } from "../auth/AuthContext";
import ProjectSelect from "../components/ProjectSelect";
import DataTable, { type DataTableColumn } from "../components/DataTable";
import EmptyState from "../components/EmptyState";
import { DEFECT_STATUS, PRIORITIES, SEVERITIES, type Defect, type Paged } from "../types";

const FETCH_ALL_PAGE_SIZE = 200;

/** Roles con policy ManageDefects (Program.cs). */
const MANAGE_ROLES = ["Administrador", "QA", "Desarrollador", "LiderTecnico"] as const;

/** Transiciones ofrecidas por estado actual (alineadas a Defect entity + ChangeDefectStatusCommand). */
function nextTransitions(status: number): { target: number; label: string; needsAssignee?: boolean }[] {
  switch (status) {
    case 1: // New
      return [
        { target: 2, label: "Asignar", needsAssignee: true },
        { target: 8, label: "Rechazar" },
      ];
    case 2: // Assigned
      return [
        { target: 3, label: "Iniciar progreso" },
        { target: 4, label: "Resolver" },
        { target: 2, label: "Reasignar", needsAssignee: true },
        { target: 8, label: "Rechazar" },
      ];
    case 3: // InProgress
      return [
        { target: 4, label: "Resolver" },
        { target: 8, label: "Rechazar" },
      ];
    case 4: // Resolved
      return [
        { target: 5, label: "Verificar" },
        { target: 6, label: "Cerrar" },
        { target: 7, label: "Reabrir" },
      ];
    case 5: // Verified
      return [
        { target: 6, label: "Cerrar" },
        { target: 7, label: "Reabrir" },
      ];
    case 6: // Closed
      return [{ target: 7, label: "Reabrir" }];
    case 7: // Reopened
      return [
        { target: 2, label: "Asignar", needsAssignee: true },
        { target: 3, label: "Iniciar progreso" },
        { target: 4, label: "Resolver" },
        { target: 8, label: "Rechazar" },
      ];
    case 8: // Rejected
      return [{ target: 6, label: "Cerrar" }];
    default:
      return [];
  }
}

interface UserOption {
  id: string;
  fullName: string;
  email: string;
}

const emptyForm = {
  title: "",
  description: "",
  severity: 3,
  priority: 2,
  sprint: "",
  version: "",
  stackTrace: "",
};

function apiError(err: unknown, fallback: string): string {
  return (err as { response?: { data?: { error?: string } } }).response?.data?.error ?? fallback;
}

export default function DefectsPage() {
  const { auth, hasRole } = useAuth();
  const canManage = hasRole(...MANAGE_ROLES);

  const [projectId, setProjectId] = useState("");
  const [data, setData] = useState<Paged<Defect> | null>(null);
  const [loadError, setLoadError] = useState(false);
  const [info, setInfo] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const [createOpen, setCreateOpen] = useState(false);
  const [form, setForm] = useState(emptyForm);
  const [busy, setBusy] = useState(false);

  const [menuAnchor, setMenuAnchor] = useState<null | HTMLElement>(null);
  const [menuDefect, setMenuDefect] = useState<Defect | null>(null);

  const [assignOpen, setAssignOpen] = useState(false);
  const [assignTarget, setAssignTarget] = useState<Defect | null>(null);
  const [assigneeId, setAssigneeId] = useState("");
  const [users, setUsers] = useState<UserOption[]>([]);

  const load = useCallback(() => {
    if (!projectId) return;
    setData(null);
    setLoadError(false);
    api
      .get<Paged<Defect>>("/defects", { params: { projectId, page: 1, pageSize: FETCH_ALL_PAGE_SIZE } })
      .then((r) => setData(r.data))
      .catch(() => setLoadError(true));
  }, [projectId]);

  useEffect(() => { load(); }, [load]);

  const userOptions = useMemo(() => {
    const self: UserOption | null = auth
      ? { id: auth.userId, fullName: `${auth.fullName} (yo)`, email: auth.email }
      : null;
    const map = new Map<string, UserOption>();
    if (self) map.set(self.id, self);
    for (const u of users) map.set(u.id, u);
    return [...map.values()];
  }, [auth, users]);

  const openCreate = () => {
    setForm(emptyForm);
    setError(null);
    setCreateOpen(true);
  };

  const createDefect = async () => {
    if (!projectId || !form.title.trim() || !form.description.trim()) {
      setError("Título y descripción son obligatorios.");
      return;
    }
    setBusy(true);
    setError(null);
    setInfo(null);
    try {
      await api.post("/defects", {
        projectId,
        title: form.title.trim(),
        description: form.description.trim(),
        severity: form.severity,
        priority: form.priority,
        moduleId: null,
        testResultId: null,
        sprint: form.sprint.trim() || null,
        version: form.version.trim() || null,
        stackTrace: form.stackTrace.trim() || null,
      });
      setCreateOpen(false);
      setInfo("Defecto registrado.");
      load();
    } catch (err: unknown) {
      setError(apiError(err, "No fue posible registrar el defecto."));
    } finally {
      setBusy(false);
    }
  };

  const openMenu = (e: React.MouseEvent<HTMLElement>, d: Defect) => {
    setMenuAnchor(e.currentTarget);
    setMenuDefect(d);
  };

  const closeMenu = () => {
    setMenuAnchor(null);
    setMenuDefect(null);
  };

  const loadAssignees = async () => {
    try {
      const { data: page } = await api.get<Paged<UserOption>>("/admin/users", {
        params: { page: 1, pageSize: 100 },
      });
      setUsers(page.items.map((u) => ({ id: u.id, fullName: u.fullName, email: u.email })));
    } catch {
      setUsers([]);
    }
  };

  const changeStatus = async (defect: Defect, targetStatus: number, assignToUserId?: string | null) => {
    setError(null);
    setInfo(null);
    try {
      await api.post(`/defects/${defect.id}/status`, {
        id: defect.id,
        targetStatus,
        assignToUserId: assignToUserId ?? null,
      });
      setInfo(`${defect.code}: estado → ${DEFECT_STATUS[targetStatus] ?? targetStatus}.`);
      load();
    } catch (err: unknown) {
      setError(apiError(err, "No fue posible cambiar el estado del defecto."));
    }
  };

  const onPickTransition = async (target: number, needsAssignee?: boolean) => {
    const defect = menuDefect;
    closeMenu();
    if (!defect) return;
    if (needsAssignee) {
      setAssignTarget(defect);
      setAssigneeId(auth?.userId ?? "");
      setAssignOpen(true);
      void loadAssignees();
      return;
    }
    await changeStatus(defect, target);
  };

  const confirmAssign = async () => {
    if (!assignTarget || !assigneeId) {
      setError("Seleccione un usuario asignado.");
      return;
    }
    setBusy(true);
    try {
      await changeStatus(assignTarget, 2, assigneeId);
      setAssignOpen(false);
      setAssignTarget(null);
    } finally {
      setBusy(false);
    }
  };

  const severityColor = (s: number) => (s >= 4 ? "error" : s === 3 ? "warning" : "default");
  const statusColor = (s: number) =>
    s === 6 ? "success" : s === 4 || s === 5 ? "info" : s === 7 || s === 8 ? "error" : "default";

  const columns: DataTableColumn<Defect>[] = [
    { key: "code", label: "Código", sortValue: (d) => d.code },
    {
      key: "title", label: "Título", sortValue: (d) => d.title,
      render: (d) => <span className="max-w-md truncate block">{d.title}</span>,
    },
    {
      key: "severity", label: "Severidad", sortValue: (d) => d.severity,
      render: (d) => <Chip label={SEVERITIES[d.severity]} size="small" color={severityColor(d.severity)} />,
    },
    { key: "priority", label: "Prioridad", sortValue: (d) => PRIORITIES[d.priority] ?? String(d.priority) },
    {
      key: "status", label: "Estado", sortValue: (d) => d.status,
      render: (d) => (
        <Chip label={DEFECT_STATUS[d.status]} size="small" color={statusColor(d.status)} variant="outlined" />
      ),
    },
    { key: "sprint", label: "Sprint", sortValue: (d) => d.sprint ?? "" },
    { key: "version", label: "Versión", sortValue: (d) => d.version ?? "" },
    { key: "createdAt", label: "Creado", sortValue: (d) => d.createdAt, render: (d) => d.createdAt.slice(0, 10) },
  ];

  const transitions = menuDefect ? nextTransitions(menuDefect.status) : [];

  return (
    <div className="flex flex-col gap-4">
      <Box className="flex items-center justify-between">
        <Typography variant="h5" fontWeight={700}>
          Gestión de defectos
        </Typography>
        <Box className="flex items-center gap-2">
          {canManage && (
            <Button variant="contained" size="small" startIcon={<AddIcon />}
              disabled={!projectId} onClick={openCreate}>
              Nuevo defecto
            </Button>
          )}
          <ProjectSelect value={projectId} onChange={(id) => setProjectId(id)} />
        </Box>
      </Box>

      {info && <Alert severity="success" onClose={() => setInfo(null)}>{info}</Alert>}
      {error && <Alert severity="error" onClose={() => setError(null)}>{error}</Alert>}

      {!canManage && (
        <Alert severity="info">
          Puede consultar defectos. Crear y cambiar estado requiere rol QA, Desarrollador, Líder técnico o Administrador.
        </Alert>
      )}

      {!projectId ? (
        <EmptyState variant="no-selection" description="Elija un proyecto arriba para ver sus defectos." />
      ) : loadError ? (
        <EmptyState variant="error" onRetry={load} />
      ) : !data ? (
        <EmptyState variant="loading" />
      ) : (
        <DataTable
          aria-label="Tabla de defectos"
          columns={columns}
          rows={data.items}
          getRowKey={(d) => d.id}
          searchPlaceholder="Buscar por código, título, sprint…"
          totalOnServer={data.totalCount}
          emptyMessage="Este proyecto no tiene defectos registrados."
          rowActions={canManage ? (d) => (
            <Tooltip title="Cambiar estado">
              <IconButton size="small" aria-label={`Acciones de ${d.code}`}
                onClick={(e) => openMenu(e, d)}>
                <MoreVertIcon fontSize="small" />
              </IconButton>
            </Tooltip>
          ) : undefined}
        />
      )}

      <Menu anchorEl={menuAnchor} open={!!menuAnchor} onClose={closeMenu}>
        {transitions.length === 0 && (
          <MenuItem disabled>
            <ListItemText primary="Sin transiciones disponibles" />
          </MenuItem>
        )}
        {transitions.map((t) => (
          <MenuItem key={`${t.target}-${t.label}`} onClick={() => void onPickTransition(t.target, t.needsAssignee)}>
            <ListItemText primary={t.label} secondary={DEFECT_STATUS[t.target]} />
          </MenuItem>
        ))}
      </Menu>

      <Dialog open={createOpen} onClose={() => setCreateOpen(false)} fullWidth maxWidth="sm">
        <DialogTitle>Nuevo defecto</DialogTitle>
        <DialogContent className="flex flex-col gap-3 pt-2">
          <TextField label="Título" required fullWidth value={form.title}
            onChange={(e) => setForm({ ...form, title: e.target.value })} />
          <TextField label="Descripción" required fullWidth multiline minRows={3} value={form.description}
            onChange={(e) => setForm({ ...form, description: e.target.value })} />
          <Box className="flex gap-3">
            <TextField select fullWidth label="Severidad" value={form.severity}
              onChange={(e) => setForm({ ...form, severity: Number(e.target.value) })}>
              {Object.entries(SEVERITIES).map(([k, v]) => (
                <MenuItem key={k} value={Number(k)}>{v}</MenuItem>
              ))}
            </TextField>
            <TextField select fullWidth label="Prioridad" value={form.priority}
              onChange={(e) => setForm({ ...form, priority: Number(e.target.value) })}>
              {Object.entries(PRIORITIES).map(([k, v]) => (
                <MenuItem key={k} value={Number(k)}>{v}</MenuItem>
              ))}
            </TextField>
          </Box>
          <Box className="flex gap-3">
            <TextField label="Sprint" fullWidth value={form.sprint}
              onChange={(e) => setForm({ ...form, sprint: e.target.value })} />
            <TextField label="Versión" fullWidth value={form.version}
              onChange={(e) => setForm({ ...form, version: e.target.value })} />
          </Box>
          <TextField label="Stack trace (opcional)" fullWidth multiline minRows={2} value={form.stackTrace}
            onChange={(e) => setForm({ ...form, stackTrace: e.target.value })} />
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setCreateOpen(false)}>Cancelar</Button>
          <Button variant="contained" disabled={busy} onClick={() => void createDefect()}>
            Registrar
          </Button>
        </DialogActions>
      </Dialog>

      <Dialog open={assignOpen} onClose={() => setAssignOpen(false)} fullWidth maxWidth="xs">
        <DialogTitle>
          Asignar {assignTarget?.code}
        </DialogTitle>
        <DialogContent className="flex flex-col gap-3 pt-2">
          <TextField select fullWidth label="Asignar a" value={assigneeId}
            onChange={(e) => setAssigneeId(e.target.value)}
            helperText={users.length === 0
              ? "Lista de usuarios no disponible; puede asignarse a usted mismo."
              : undefined}>
            {userOptions.map((u) => (
              <MenuItem key={u.id} value={u.id}>{u.fullName} — {u.email}</MenuItem>
            ))}
          </TextField>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setAssignOpen(false)}>Cancelar</Button>
          <Button variant="contained" disabled={busy || !assigneeId} onClick={() => void confirmAssign()}>
            Asignar
          </Button>
        </DialogActions>
      </Dialog>
    </div>
  );
}
