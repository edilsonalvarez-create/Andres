import { useCallback, useEffect, useState } from "react";
import {
  Box, Button, Chip, IconButton, Paper, Table, TableBody, TableCell, TableContainer,
  TableHead, TablePagination, TableRow, Tooltip, Typography,
} from "@mui/material";
import AddIcon from "@mui/icons-material/Add";
import EditIcon from "@mui/icons-material/Edit";
import { api } from "../api/client";
import ProjectSelect from "../components/ProjectSelect";
import ScriptEditorDialog from "../components/ScriptEditorDialog";
import { FRAMEWORKS, PRIORITIES, TEST_TYPES, type Paged, type TestCase } from "../types";

export default function TestCasesPage() {
  const [projectId, setProjectId] = useState("");
  const [data, setData] = useState<Paged<TestCase> | null>(null);
  const [page, setPage] = useState(0);
  const [editorOpen, setEditorOpen] = useState(false);
  const [editing, setEditing] = useState<TestCase | null>(null);

  const load = useCallback(() => {
    if (!projectId) return;
    api
      .get<Paged<TestCase>>("/testcases", { params: { projectId, page: page + 1, pageSize: 15 } })
      .then((r) => setData(r.data));
  }, [projectId, page]);

  useEffect(() => { load(); }, [load]);

  const openNew = () => { setEditing(null); setEditorOpen(true); };
  const openEdit = (tc: TestCase) => { setEditing(tc); setEditorOpen(true); };

  return (
    <div className="flex flex-col gap-4">
      <Box className="flex items-center justify-between">
        <Typography variant="h5" fontWeight={700}>
          Casos de prueba
        </Typography>
        <Box className="flex items-center gap-2">
          <Button variant="contained" size="small" startIcon={<AddIcon />}
            disabled={!projectId} onClick={openNew}>
            Nuevo script
          </Button>
          <ProjectSelect value={projectId} onChange={(id) => { setProjectId(id); setPage(0); }} />
        </Box>
      </Box>

      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Código</TableCell>
              <TableCell>Título</TableCell>
              <TableCell>Tipo</TableCell>
              <TableCell>Prioridad</TableCell>
              <TableCell>Automatización</TableCell>
              <TableCell align="center">Pasos</TableCell>
              <TableCell align="center">Script</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {data?.items.map((tc) => (
              <TableRow key={tc.id} hover>
                <TableCell>{tc.code}</TableCell>
                <TableCell>{tc.title}</TableCell>
                <TableCell>
                  <Chip label={TEST_TYPES[tc.type] ?? tc.type} size="small" variant="outlined" />
                </TableCell>
                <TableCell>
                  <Chip
                    label={PRIORITIES[tc.priority] ?? tc.priority}
                    size="small"
                    color={tc.priority >= 4 ? "error" : tc.priority === 3 ? "warning" : "default"}
                  />
                </TableCell>
                <TableCell>
                  <Chip
                    label={FRAMEWORKS[tc.framework] ?? tc.framework}
                    size="small"
                    color={tc.framework === 0 ? "default" : "success"}
                    variant={tc.framework === 0 ? "outlined" : "filled"}
                  />
                </TableCell>
                <TableCell align="center">{tc.steps.length}</TableCell>
                <TableCell align="center">
                  <Tooltip title="Editar script">
                    <IconButton size="small" onClick={() => openEdit(tc)}>
                      <EditIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
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
          rowsPerPage={15}
          rowsPerPageOptions={[15]}
        />
      </TableContainer>

      <ScriptEditorDialog
        projectId={projectId}
        testCase={editing}
        open={editorOpen}
        onClose={() => setEditorOpen(false)}
        onSaved={load}
      />
    </div>
  );
}
