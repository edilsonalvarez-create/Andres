import { useEffect, useState } from "react";
import {
  Box, Chip, Paper, Table, TableBody, TableCell, TableContainer,
  TableHead, TablePagination, TableRow, Typography,
} from "@mui/material";
import { api } from "../api/client";
import ProjectSelect from "../components/ProjectSelect";
import { DEFECT_STATUS, PRIORITIES, SEVERITIES, type Defect, type Paged } from "../types";

export default function DefectsPage() {
  const [projectId, setProjectId] = useState("");
  const [data, setData] = useState<Paged<Defect> | null>(null);
  const [page, setPage] = useState(0);

  useEffect(() => {
    if (!projectId) return;
    api
      .get<Paged<Defect>>("/defects", { params: { projectId, page: page + 1, pageSize: 15 } })
      .then((r) => setData(r.data));
  }, [projectId, page]);

  const severityColor = (s: number) => (s >= 4 ? "error" : s === 3 ? "warning" : "default");
  const statusColor = (s: number) =>
    s === 6 ? "success" : s === 4 || s === 5 ? "info" : s === 7 ? "error" : "default";

  return (
    <div className="flex flex-col gap-4">
      <Box className="flex items-center justify-between">
        <Typography variant="h5" fontWeight={700}>
          Gestión de defectos
        </Typography>
        <ProjectSelect value={projectId} onChange={(id) => { setProjectId(id); setPage(0); }} />
      </Box>

      <TableContainer component={Paper}>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Código</TableCell>
              <TableCell>Título</TableCell>
              <TableCell>Severidad</TableCell>
              <TableCell>Prioridad</TableCell>
              <TableCell>Estado</TableCell>
              <TableCell>Sprint</TableCell>
              <TableCell>Versión</TableCell>
              <TableCell>Creado</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {data?.items.map((d) => (
              <TableRow key={d.id} hover>
                <TableCell>{d.code}</TableCell>
                <TableCell className="max-w-md truncate">{d.title}</TableCell>
                <TableCell>
                  <Chip label={SEVERITIES[d.severity]} size="small" color={severityColor(d.severity)} />
                </TableCell>
                <TableCell>{PRIORITIES[d.priority]}</TableCell>
                <TableCell>
                  <Chip label={DEFECT_STATUS[d.status]} size="small" color={statusColor(d.status)} variant="outlined" />
                </TableCell>
                <TableCell>{d.sprint ?? "—"}</TableCell>
                <TableCell>{d.version ?? "—"}</TableCell>
                <TableCell>{d.createdAt.slice(0, 10)}</TableCell>
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
    </div>
  );
}
