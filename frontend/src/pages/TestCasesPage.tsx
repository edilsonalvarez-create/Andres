import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle,
  IconButton, MenuItem, TextField, Tooltip, Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import LinkIcon from "@mui/icons-material/Link";
import { Link as RouterLink } from "react-router-dom";
import { api, extractApiErrorMessage } from "../api/client";
import { loadProjectUserStories } from "../api/catalogStories";
import ProjectSelect from "../components/ProjectSelect";
import ScriptEditorDialog from "../components/ScriptEditorDialog";
import DataTable, { type DataTableColumn } from "../components/DataTable";
import EmptyState from "../components/EmptyState";
import { FRAMEWORKS, PRIORITIES, TEST_TYPES, type Paged, type TestCase, type UserStory } from "../types";

const FETCH_ALL_PAGE_SIZE = 200;

export default function TestCasesPage() {
  const [projectId, setProjectId] = useState("");
  const [data, setData] = useState<Paged<TestCase> | null>(null);
  const [stories, setStories] = useState<UserStory[]>([]);
  const [loadError, setLoadError] = useState(false);
  const [editorOpen, setEditorOpen] = useState(false);
  const [editing, setEditing] = useState<TestCase | null>(null);
  const [linkTarget, setLinkTarget] = useState<TestCase | null>(null);
  const [selectedStoryId, setSelectedStoryId] = useState("");
  const [linkBusy, setLinkBusy] = useState(false);
  const [linkError, setLinkError] = useState<string | null>(null);
  const [linkInfo, setLinkInfo] = useState<string | null>(null);

  const storyById = useMemo(
    () => new Map(stories.map((s) => [s.id, s])),
    [stories]);

  const load = useCallback(() => {
    if (!projectId) return;
    setData(null);
    setLoadError(false);
    api
      .get<Paged<TestCase>>("/testcases", { params: { projectId, page: 1, pageSize: FETCH_ALL_PAGE_SIZE } })
      .then((r) => setData(r.data))
      .catch(() => setLoadError(true));
    loadProjectUserStories(projectId)
      .then(setStories)
      .catch(() => setStories([]));
  }, [projectId]);

  useEffect(() => { load(); }, [load]);

  const openNew = () => { setEditing(null); setEditorOpen(true); };
  const openEdit = (tc: TestCase) => { setEditing(tc); setEditorOpen(true); };

  const openLink = (tc: TestCase) => {
    setLinkTarget(tc);
    setSelectedStoryId(tc.userStoryId ?? "");
    setLinkError(null);
  };

  const saveLink = async () => {
    if (!linkTarget) return;
    setLinkBusy(true);
    setLinkError(null);
    setLinkInfo(null);
    try {
      await api.put(`/testcases/${linkTarget.id}/user-story`, {
        id: linkTarget.id,
        userStoryId: selectedStoryId || null,
      });
      setLinkTarget(null);
      setLinkInfo(selectedStoryId
        ? "Caso vinculado a la historia. Revise Trazabilidad para ver la cobertura."
        : "Vínculo con historia eliminado.");
      load();
    } catch (err: unknown) {
      setLinkError(extractApiErrorMessage(err) ?? "No fue posible vincular la historia de usuario.");
    } finally {
      setLinkBusy(false);
    }
  };

  const columns: DataTableColumn<TestCase>[] = [
    { key: "code", label: "Código", sortValue: (tc) => tc.code },
    { key: "title", label: "Título", sortValue: (tc) => tc.title },
    {
      key: "userStory",
      label: "Historia",
      sortValue: (tc) => storyById.get(tc.userStoryId ?? "")?.title ?? "",
      render: (tc) => {
        const story = tc.userStoryId ? storyById.get(tc.userStoryId) : undefined;
        return story
          ? <Chip label={story.title} size="small" color="primary" variant="outlined" />
          : <Typography variant="caption" color="text.secondary">Sin vínculo</Typography>;
      },
    },
    {
      key: "type", label: "Tipo", sortValue: (tc) => TEST_TYPES[tc.type] ?? String(tc.type),
      render: (tc) => <Chip label={TEST_TYPES[tc.type] ?? tc.type} size="small" variant="outlined" />,
    },
    {
      key: "priority", label: "Prioridad", sortValue: (tc) => tc.priority,
      render: (tc) => (
        <Chip label={PRIORITIES[tc.priority] ?? tc.priority} size="small"
          color={tc.priority >= 4 ? "error" : tc.priority === 3 ? "warning" : "default"} />
      ),
    },
    {
      key: "framework", label: "Automatización", sortValue: (tc) => FRAMEWORKS[tc.framework] ?? String(tc.framework),
      render: (tc) => (
        <Chip label={FRAMEWORKS[tc.framework] ?? tc.framework} size="small"
          color={tc.framework === 0 ? "default" : "success"}
          variant={tc.framework === 0 ? "outlined" : "filled"} />
      ),
    },
    { key: "steps", label: "Pasos", align: "center", sortValue: (tc) => tc.steps.length },
  ];

  return (
    <div className="flex flex-col gap-4">
      <Box className="flex items-center justify-between">
        <Typography variant="h5" fontWeight={700}>
          Casos de prueba
        </Typography>
        <Box className="flex items-center gap-2">
          <Button component={RouterLink} to="/trazabilidad" size="small" variant="outlined"
            disabled={!projectId}>
            Ver trazabilidad
          </Button>
          <Button variant="contained" size="small" startIcon={<AddIcon />}
            disabled={!projectId} onClick={openNew}>
            Nuevo script
          </Button>
          <ProjectSelect value={projectId} onChange={(id) => setProjectId(id)} />
        </Box>
      </Box>

      {linkInfo && <Alert severity="success" onClose={() => setLinkInfo(null)}>{linkInfo}</Alert>}

      {!projectId ? (
        <EmptyState variant="no-selection" description="Elija un proyecto arriba para ver sus casos de prueba." />
      ) : loadError ? (
        <EmptyState variant="error" onRetry={load} />
      ) : !data ? (
        <EmptyState variant="loading" />
      ) : (
        <DataTable
          aria-label="Tabla de casos de prueba"
          columns={columns}
          rows={data.items}
          getRowKey={(tc) => tc.id}
          searchPlaceholder="Buscar por código o título…"
          totalOnServer={data.totalCount}
          emptyMessage="Este proyecto no tiene casos de prueba. Cree el primero con “Nuevo script”."
          rowActions={(tc) => (
            <>
              <Tooltip title="Vincular historia de usuario">
                <IconButton size="small" aria-label={`Vincular historia de ${tc.title}`}
                  onClick={() => openLink(tc)}>
                  <LinkIcon fontSize="small" />
                </IconButton>
              </Tooltip>
              <Tooltip title="Editar script">
                <IconButton size="small" aria-label={`Editar script de ${tc.title}`} onClick={() => openEdit(tc)}>
                  <EditIcon fontSize="small" />
                </IconButton>
              </Tooltip>
            </>
          )}
        />
      )}

      <ScriptEditorDialog
        projectId={projectId}
        testCase={editing}
        open={editorOpen}
        onClose={() => setEditorOpen(false)}
        onSaved={load}
      />

      <Dialog open={!!linkTarget} onClose={() => setLinkTarget(null)} fullWidth maxWidth="sm">
        <DialogTitle>Vincular historia de usuario</DialogTitle>
        <DialogContent className="flex flex-col gap-3 pt-2">
          {linkTarget && (
            <Typography variant="body2" color="text.secondary">
              Caso <strong>{linkTarget.code}</strong> — {linkTarget.title}
            </Typography>
          )}
          {stories.length === 0 ? (
            <Alert severity="info">
              No hay historias en el catálogo de este proyecto. Cree módulos, requerimientos e historias
              en <RouterLink to="/catalogo">Catálogo</RouterLink> y vuelva a vincular.
            </Alert>
          ) : (
            <TextField
              select fullWidth label="Historia de usuario"
              value={selectedStoryId}
              onChange={(e) => setSelectedStoryId(e.target.value)}
              helperText="La cobertura en Trazabilidad usa este vínculo."
            >
              <MenuItem value="">Sin vínculo</MenuItem>
              {stories.map((s) => (
                <MenuItem key={s.id} value={s.id}>{s.title}</MenuItem>
              ))}
            </TextField>
          )}
          {linkError && <Alert severity="error">{linkError}</Alert>}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setLinkTarget(null)}>Cancelar</Button>
          <Button variant="contained"
            disabled={linkBusy || (stories.length === 0 && !linkTarget?.userStoryId)}
            onClick={() => void saveLink()}>
            Guardar vínculo
          </Button>
        </DialogActions>
      </Dialog>
    </div>
  );
}
