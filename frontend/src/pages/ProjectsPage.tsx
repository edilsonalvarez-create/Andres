import { useEffect, useState, type FormEvent } from "react";
import {
  Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle,
  Paper, Table, TableBody, TableCell, TableContainer, TableHead, TablePagination,
  TableRow, TextField, Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import { api } from "../api/client";
import type { Paged, Project } from "../types";

export default function ProjectsPage() {
  const [data, setData] = useState<Paged<Project> | null>(null);
  const [page, setPage] = useState(0);
  const [search, setSearch] = useState("");
  const [dialogOpen, setDialogOpen] = useState(false);
  const [form, setForm] = useState({ code: "", name: "", description: "", repositoryUrl: "" });
  const [error, setError] = useState<string | null>(null);

  const load = () =>
    api
      .get<Paged<Project>>("/projects", { params: { page: page + 1, pageSize: 10, search: search || undefined } })
      .then((r) => setData(r.data))
      .catch(() => setError("No fue posible cargar los proyectos."));

  useEffect(() => {
    void load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, search]);

  const handleCreate = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      await api.post("/projects", {
        ...form,
        description: form.description || null,
        repositoryUrl: form.repositoryUrl || null,
      });
      setDialogOpen(false);
      setForm({ code: "", name: "", description: "", repositoryUrl: "" });
      void load();
    } catch (err: unknown) {
      const detail = (err as { response?: { data?: { error?: string } } }).response?.data?.error;
      setError(detail ?? "No fue posible crear el proyecto.");
    }
  };

  return (
    <div className="flex flex-col gap-4">
      <Box className="flex items-center justify-between">
        <Typography variant="h5" fontWeight={700}>
          Proyectos
        </Typography>
        <Button variant="contained" startIcon={<AddIcon />} onClick={() => setDialogOpen(true)}>
          Nuevo proyecto
        </Button>
      </Box>

      {error && <Alert severity="error" onClose={() => setError(null)}>{error}</Alert>}

      <TextField
        label="Buscar por nombre o código"
        size="small"
        value={search}
        onChange={(e) => { setSearch(e.target.value); setPage(0); }}
        className="max-w-sm"
      />

      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Código</TableCell>
              <TableCell>Nombre</TableCell>
              <TableCell>Descripción</TableCell>
              <TableCell align="center">Módulos</TableCell>
              <TableCell align="center">Estado</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {data?.items.map((p) => (
              <TableRow key={p.id} hover>
                <TableCell><Chip label={p.code} size="small" color="primary" variant="outlined" /></TableCell>
                <TableCell>{p.name}</TableCell>
                <TableCell className="max-w-md truncate">{p.description}</TableCell>
                <TableCell align="center">{p.modulesCount}</TableCell>
                <TableCell align="center">
                  <Chip
                    label={p.isActive ? "Activo" : "Inactivo"}
                    size="small"
                    color={p.isActive ? "success" : "default"}
                  />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
        <TablePagination
          component="div"
          count={data?.totalCount ?? 0}
          page={page}
          onPageChange={(_, newPage) => setPage(newPage)}
          rowsPerPage={10}
          rowsPerPageOptions={[10]}
        />
      </TableContainer>

      <Dialog open={dialogOpen} onClose={() => setDialogOpen(false)} maxWidth="sm" fullWidth>
        <form onSubmit={handleCreate}>
          <DialogTitle>Nuevo proyecto</DialogTitle>
          <DialogContent className="flex flex-col gap-4 pt-2">
            <TextField label="Código" required inputProps={{ maxLength: 20 }}
              value={form.code} onChange={(e) => setForm({ ...form, code: e.target.value })} />
            <TextField label="Nombre" required
              value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
            <TextField label="Descripción" multiline rows={2}
              value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
            <TextField label="URL del repositorio" type="url"
              value={form.repositoryUrl} onChange={(e) => setForm({ ...form, repositoryUrl: e.target.value })} />
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setDialogOpen(false)}>Cancelar</Button>
            <Button type="submit" variant="contained">Crear</Button>
          </DialogActions>
        </form>
      </Dialog>
    </div>
  );
}
