import { useCallback, useEffect, useState } from "react";
import {
  Alert, Box, Button, Card, CardContent, Chip, Grid2 as Grid, IconButton, List, ListItemButton,
  ListItemText, MenuItem, TextField, Tooltip, Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import RocketLaunchIcon from "@mui/icons-material/RocketLaunch";
import { api, extractApiErrorMessage } from "../api/client";
import ProjectSelect from "../components/ProjectSelect";

interface Module { id: string; name: string; description?: string }
interface Requirement { id: string; code: string; title: string; description?: string }
interface UserStory { id: string; title: string; acceptanceCriteria?: string }
interface Version { id: string; number: string; notes?: string; releasedAt?: string }

export default function CatalogPage() {
  const [projectId, setProjectId] = useState("");
  const [message, setMessage] = useState<string | null>(null);

  const [modules, setModules] = useState<Module[]>([]);
  const [moduleId, setModuleId] = useState("");
  const [requirements, setRequirements] = useState<Requirement[]>([]);
  const [requirementId, setRequirementId] = useState("");
  const [stories, setStories] = useState<UserStory[]>([]);
  const [versions, setVersions] = useState<Version[]>([]);

  // Formularios
  const [moduleForm, setModuleForm] = useState({ name: "", description: "" });
  const [reqForm, setReqForm] = useState({ code: "", title: "", description: "" });
  const [storyForm, setStoryForm] = useState({ title: "", acceptanceCriteria: "" });
  const [versionForm, setVersionForm] = useState({ number: "", notes: "" });

  const loadModules = useCallback(() => {
    if (!projectId) return;
    api.get<Module[]>(`/catalog/projects/${projectId}/modules`).then((r) => setModules(r.data));
    api.get<Version[]>(`/catalog/projects/${projectId}/versions`).then((r) => setVersions(r.data));
    setModuleId(""); setRequirements([]); setRequirementId(""); setStories([]);
  }, [projectId]);

  useEffect(() => { loadModules(); }, [loadModules]);

  const loadRequirements = useCallback(() => {
    if (!moduleId) { setRequirements([]); return; }
    api.get<Requirement[]>(`/catalog/modules/${moduleId}/requirements`).then((r) => setRequirements(r.data));
    setRequirementId(""); setStories([]);
  }, [moduleId]);

  useEffect(() => { loadRequirements(); }, [loadRequirements]);

  const loadStories = useCallback(() => {
    if (!requirementId) { setStories([]); return; }
    api.get<UserStory[]>(`/catalog/requirements/${requirementId}/stories`).then((r) => setStories(r.data));
  }, [requirementId]);

  useEffect(() => { loadStories(); }, [loadStories]);

  const showError = (err: unknown, fallback: string) =>
    setMessage(extractApiErrorMessage(err) ?? fallback);

  const createModule = async () => {
    try {
      await api.post(`/projects/${projectId}/modules`, {
        projectId, name: moduleForm.name, description: moduleForm.description || null,
      });
      setModuleForm({ name: "", description: "" });
      loadModules();
    } catch (e) { showError(e, "No se pudo crear el módulo."); }
  };

  const createRequirement = async () => {
    try {
      await api.post("/catalog/requirements", { moduleId, ...reqForm, description: reqForm.description || null });
      setReqForm({ code: "", title: "", description: "" });
      loadRequirements();
    } catch (e) { showError(e, "No se pudo crear el requerimiento."); }
  };

  const createStory = async () => {
    try {
      await api.post("/catalog/stories", {
        requirementId, ...storyForm, acceptanceCriteria: storyForm.acceptanceCriteria || null,
      });
      setStoryForm({ title: "", acceptanceCriteria: "" });
      loadStories();
    } catch (e) { showError(e, "No se pudo crear la historia."); }
  };

  const createVersion = async () => {
    try {
      await api.post("/catalog/versions", { projectId, ...versionForm, notes: versionForm.notes || null });
      setVersionForm({ number: "", notes: "" });
      api.get<Version[]>(`/catalog/projects/${projectId}/versions`).then((r) => setVersions(r.data));
    } catch (e) { showError(e, "No se pudo crear la versión."); }
  };

  const releaseVersion = async (id: string) => {
    await api.post(`/catalog/versions/${id}/release`);
    api.get<Version[]>(`/catalog/projects/${projectId}/versions`).then((r) => setVersions(r.data));
  };

  return (
    <div className="flex flex-col gap-4">
      <Box className="flex items-center justify-between">
        <Typography variant="h5" fontWeight={700}>Catálogo del proyecto</Typography>
        <ProjectSelect value={projectId} onChange={setProjectId} />
      </Box>

      {message && <Alert severity="error" onClose={() => setMessage(null)}>{message}</Alert>}

      <Grid container spacing={2}>
        {/* Módulos */}
        <Grid size={{ xs: 12 }}>
          <Card>
            <CardContent className="flex flex-col gap-3">
              <Box>
                <Typography variant="subtitle1" fontWeight={600}>Módulos</Typography>
                <Typography variant="body2" color="text.secondary">
                  Un módulo es un área funcional del proyecto (p. ej. Login, Reportes, Facturación). Agrupa sus
                  requerimientos e historias, y a él se enlazan los casos de prueba. Crea aquí los módulos del proyecto:
                  son la fuente que alimenta el desplegable de abajo.
                </Typography>
              </Box>

              {!projectId && (
                <Typography variant="body2" color="text.secondary">Selecciona un proyecto para gestionar sus módulos.</Typography>
              )}

              {projectId && (
                <Grid container spacing={2}>
                  <Grid size={{ xs: 12, md: 6 }}>
                    <List dense className="border rounded max-h-52 overflow-auto">
                      {modules.map((m) => (
                        <ListItemText key={m.id} className="px-2 py-1" primary={m.name} secondary={m.description} />
                      ))}
                      {modules.length === 0 && <ListItemText className="p-2" secondary="El proyecto aún no tiene módulos" />}
                    </List>
                  </Grid>
                  <Grid size={{ xs: 12, md: 6 }}>
                    <Box className="flex flex-col gap-2">
                      <TextField size="small" label="Nombre del módulo" value={moduleForm.name}
                        onChange={(e) => setModuleForm({ ...moduleForm, name: e.target.value })} fullWidth />
                      <TextField size="small" label="Descripción (opcional)" value={moduleForm.description}
                        onChange={(e) => setModuleForm({ ...moduleForm, description: e.target.value })}
                        multiline minRows={2} fullWidth />
                      <Button size="small" variant="contained" startIcon={<AddIcon />}
                        disabled={!moduleForm.name} onClick={() => void createModule()}>
                        Crear módulo
                      </Button>
                    </Box>
                  </Grid>
                </Grid>
              )}
            </CardContent>
          </Card>
        </Grid>

        {/* Requerimientos e historias */}
        <Grid size={{ xs: 12, md: 8 }}>
          <Card>
            <CardContent className="flex flex-col gap-3">
              <Typography variant="subtitle1" fontWeight={600}>Requerimientos e historias</Typography>
              <TextField select size="small" label="Módulo" value={moduleId}
                onChange={(e) => setModuleId(e.target.value)} className="max-w-sm">
                {modules.map((m) => <MenuItem key={m.id} value={m.id}>{m.name}</MenuItem>)}
                {modules.length === 0 && <MenuItem disabled value="">El proyecto no tiene módulos</MenuItem>}
              </TextField>

              {moduleId && (
                <Grid container spacing={2}>
                  <Grid size={{ xs: 12, sm: 5 }}>
                    <Typography variant="body2" fontWeight={600} gutterBottom>Requerimientos</Typography>
                    <List dense className="border rounded max-h-64 overflow-auto">
                      {requirements.map((r) => (
                        <ListItemButton key={r.id} selected={requirementId === r.id}
                          onClick={() => setRequirementId(r.id)}>
                          <ListItemText primary={`${r.code} — ${r.title}`} secondary={r.description} />
                        </ListItemButton>
                      ))}
                      {requirements.length === 0 && <ListItemText className="p-2" secondary="Sin requerimientos" />}
                    </List>
                    <Box className="flex flex-col gap-2 mt-2">
                      <Box className="flex gap-2">
                        <TextField size="small" label="Código" value={reqForm.code}
                          onChange={(e) => setReqForm({ ...reqForm, code: e.target.value })} className="w-28" />
                        <TextField size="small" label="Título" value={reqForm.title}
                          onChange={(e) => setReqForm({ ...reqForm, title: e.target.value })} fullWidth />
                      </Box>
                      <Button size="small" variant="outlined" startIcon={<AddIcon />}
                        disabled={!reqForm.code || !reqForm.title} onClick={() => void createRequirement()}>
                        Agregar requerimiento
                      </Button>
                    </Box>
                  </Grid>

                  <Grid size={{ xs: 12, sm: 7 }}>
                    <Typography variant="body2" fontWeight={600} gutterBottom>
                      Historias {requirementId ? "" : "(seleccione un requerimiento)"}
                    </Typography>
                    <List dense className="border rounded max-h-64 overflow-auto">
                      {stories.map((s) => (
                        <ListItemText key={s.id} className="px-2 py-1"
                          primary={s.title} secondary={s.acceptanceCriteria} />
                      ))}
                      {requirementId && stories.length === 0 && <ListItemText className="p-2" secondary="Sin historias" />}
                    </List>
                    {requirementId && (
                      <Box className="flex flex-col gap-2 mt-2">
                        <TextField size="small" label="Título de la historia" value={storyForm.title}
                          onChange={(e) => setStoryForm({ ...storyForm, title: e.target.value })} fullWidth />
                        <TextField size="small" label="Criterios de aceptación" value={storyForm.acceptanceCriteria}
                          onChange={(e) => setStoryForm({ ...storyForm, acceptanceCriteria: e.target.value })}
                          multiline minRows={2} fullWidth />
                        <Button size="small" variant="outlined" startIcon={<AddIcon />}
                          disabled={!storyForm.title} onClick={() => void createStory()}>
                          Agregar historia
                        </Button>
                      </Box>
                    )}
                  </Grid>
                </Grid>
              )}
            </CardContent>
          </Card>
        </Grid>

        {/* Versiones */}
        <Grid size={{ xs: 12, md: 4 }}>
          <Card>
            <CardContent className="flex flex-col gap-3">
              <Typography variant="subtitle1" fontWeight={600}>Versiones</Typography>
              <List dense className="border rounded max-h-64 overflow-auto">
                {versions.map((v) => (
                  <Box key={v.id} className="flex items-center justify-between px-2 py-1">
                    <ListItemText primary={v.number} secondary={v.notes} />
                    {v.releasedAt
                      ? <Chip label="Liberada" size="small" color="success" />
                      : (
                        <Tooltip title="Marcar liberada">
                          <IconButton size="small" onClick={() => void releaseVersion(v.id)}>
                            <RocketLaunchIcon fontSize="small" />
                          </IconButton>
                        </Tooltip>
                      )}
                  </Box>
                ))}
                {versions.length === 0 && <ListItemText className="p-2" secondary="Sin versiones" />}
              </List>
              <TextField size="small" label="Número (p. ej. 1.0.0)" value={versionForm.number}
                onChange={(e) => setVersionForm({ ...versionForm, number: e.target.value })} />
              <TextField size="small" label="Notas" value={versionForm.notes}
                onChange={(e) => setVersionForm({ ...versionForm, notes: e.target.value })} multiline minRows={2} />
              <Button size="small" variant="outlined" startIcon={<AddIcon />}
                disabled={!projectId || !versionForm.number} onClick={() => void createVersion()}>
                Agregar versión
              </Button>
            </CardContent>
          </Card>
        </Grid>
      </Grid>
    </div>
  );
}
