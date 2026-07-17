import { useEffect, useState, type FormEvent } from "react";
import {
  Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle,
  IconButton, TextField, Tooltip, Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import DeleteIcon from "@mui/icons-material/DeleteOutline";
import { api } from "../api/client";
import type { Paged, Project } from "../types";
import DataTable, { type DataTableColumn } from "../components/DataTable";
import EmptyState from "../components/EmptyState";
import ConfirmDialog from "../components/ConfirmDialog";

const emptyForm = { code: "", name: "", description: "", repositoryUrl: "" };
// Techo alineado con el clamp del backend (Repository<T>.PagedAsync, máx. 200) — ver
// Design-System.md "Decisión de datos: búsqueda instantánea client-side en catálogos".
const FETCH_ALL_PAGE_SIZE = 200;

export default function ProjectsPage() {
  const [data, setData] = useState<Paged<Project> | null>(null);
  const [loadError, setLoadError] = useState(false);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState(emptyForm);
  const [error, setError] = useState<string | null>(null);
  const [deleting, setDeleting] = useState<Project | null>(null);

  const load = () => {
    setLoadError(false);
    api
      .get<Paged<Project>>("/projects", { params: { page: 1, pageSize: FETCH_ALL_PAGE_SIZE } })
      .then((r) => setData(r.data))
      .catch(() => setLoadError(true));
  };

  useEffect(() => { load(); }, []);

  const openCreate = () => {
    setEditingId(null);
    setForm(emptyForm);
    setError(null);
    setDialogOpen(true);
  };

  const openEdit = (p: Project) => {
    setEditingId(p.id);
    setForm({
      code: p.code,
      name: p.name,
      description: p.description ?? "",
      repositoryUrl: p.repositoryUrl ?? "",
    });
    setError(null);
    setDialogOpen(true);
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      if (editingId) {
        await api.put(`/projects/${editingId}`, {
          id: editingId,
          name: form.name,
          description: form.description || null,
          repositoryUrl: form.repositoryUrl || null,
        });
      } else {
        await api.post("/projects", {
          ...form,
          description: form.description || null,
          repositoryUrl: form.repositoryUrl || null,
        });
      }
      setDialogOpen(false);
      setForm(emptyForm);
      setEditingId(null);
      load();
    } catch (err: unknown) {
      const detail = (err as { response?: { data?: { error?: string } } }).response?.data?.error;
      setError(detail ?? (editingId ? "No fue posible actualizar el proyecto." : "No fue posible crear el proyecto."));
    }
  };

  const confirmDelete = async () => {
    if (!deleting) return;
    const target = deleting;
    setDeleting(null);
    setError(null);
    try {
      await api.delete(`/projects/${target.id}`);
      load();
    } catch (err: unknown) {
      const detail = (err as { response?: { data?: { error?: string } } }).response?.data?.error;
      setError(detail ?? "No fue posible eliminar el proyecto.");
    }
  };

  const columns: DataTableColumn<Project>[] = [
    {
      key: "code", label: "Código", sortValue: (p) => p.code,
      render: (p) => <Chip label={p.code} size="small" color="primary" variant="outlined" />,
    },
    { key: "name", label: "Nombre", sortValue: (p) => p.name },
    {
      key: "description", label: "Descripción", sortable: false,
      render: (p) => <span className="max-w-md truncate block">{p.description}</span>,
    },
    { key: "modules", label: "Módulos", align: "center", sortValue: (p) => p.modulesCount },
    {
      key: "status", label: "Estado", align: "center", sortValue: (p) => (p.isActive ? 1 : 0),
      render: (p) => (
        <Chip label={p.isActive ? "Activo" : "Inactivo"} size="small" color={p.isActive ? "success" : "default"} />
      ),
    },
  ];

  return (
    <div className="flex flex-col gap-4">
      <Box className="flex items-center justify-between">
        <Typography variant="h5" fontWeight={700}>
          Proyectos
        </Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={openCreate}>
          Nuevo proyecto
        </Button>
      </Box>

      {error && <Alert severity="error" onClose={() => setError(null)}>{error}</Alert>}

      {loadError ? (
        <EmptyState variant="error" onRetry={load} />
      ) : !data ? (
        <EmptyState variant="loading" />
      ) : (
        <DataTable
          aria-label="Tabla de proyectos"
          columns={columns}
          rows={data.items}
          getRowKey={(p) => p.id}
          searchPlaceholder="Buscar por nombre o código…"
          totalOnServer={data.totalCount}
          emptyMessage="Todavía no hay proyectos. Cree el primero con “Nuevo proyecto”."
          rowActions={(p) => (
            <>
              <Tooltip title="Editar">
                <IconButton size="small" aria-label={`Editar proyecto ${p.name}`} onClick={() => openEdit(p)}>
                  <EditIcon fontSize="small" />
                </IconButton>
              </Tooltip>
              <Tooltip title="Eliminar">
                <IconButton
                  size="small" color="error" aria-label={`Eliminar proyecto ${p.name}`}
                  onClick={() => setDeleting(p)}
                >
                  <DeleteIcon fontSize="small" />
                </IconButton>
              </Tooltip>
            </>
          )}
        />
      )}

      <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)} maxWidth="sm" fullWidth>
        <form onSubmit={handleSubmit}>
          <DialogTitle>{editingId ? "Editar proyecto" : "Nuevo proyecto"}</DialogTitle>
          <DialogContent className="flex flex-col gap-4 pt-2">
            <TextFieldCode value={form.code} disabled={!!editingId}
              onChange={(v) => setForm({ ...form, code: v })} />
            <TextFieldSimple label="Nombre" required value={form.name}
              onChange={(v) => setForm({ ...form, name: v })} />
            <TextFieldSimple label="Descripción" multiline rows={2} value={form.description}
              onChange={(v) => setForm({ ...form, description: v })} />
            <TextFieldSimple label="URL del repositorio" type="url" value={form.repositoryUrl}
              onChange={(v) => setForm({ ...form, repositoryUrl: v })} />
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setDialogOpen(false)}>Cancelar</Button>
            <Button type="submit" variant="contained">{editingId ? "Guardar" : "Crear"}</Button>
          </DialogActions>
        </form>
      </Dialog>

      <ConfirmDialog
        open={!!deleting}
        title="Eliminar proyecto"
        description={
          deleting
            ? `¿Eliminar el proyecto "${deleting.name}" (${deleting.code})? Esta acción lo desactiva y no se puede deshacer desde esta pantalla.`
            : ""
        }
        confirmLabel="Eliminar"
        destructive
        onConfirm={() => void confirmDelete()}
        onCancel={() => setDeleting(null)}
      />
    </div>
  );
}

// Sub-componentes locales mínimos para no repetir props de TextField en cada campo del formulario.
function TextFieldSimple({ label, value, onChange, required, multiline, rows, type }: {
  label: string; value: string; onChange: (v: string) => void;
  required?: boolean; multiline?: boolean; rows?: number; type?: string;
}) {
  return (
    <TextField
      label={label} required={required} multiline={multiline} rows={rows} type={type}
      value={value} onChange={(e) => onChange(e.target.value)}
    />
  );
}

function TextFieldCode({ value, disabled, onChange }: {
  value: string; disabled: boolean; onChange: (v: string) => void;
}) {
  return (
    <TextField
      label="Código" required inputProps={{ maxLength: 20 }}
      value={value} disabled={disabled}
      helperText={disabled ? "El código no se puede modificar." : undefined}
      onChange={(e) => onChange(e.target.value)}
    />
  );
}
