import { Fragment, useEffect, useState } from "react";
import {
  Alert, Box, Button, Chip, Collapse, IconButton, Paper, Table, TableBody, TableCell,
  TableContainer, TableHead, TableRow, Typography,
} from "@mui/material";
import KeyboardArrowDownIcon from "@mui/icons-material/KeyboardArrowDown";
import KeyboardArrowUpIcon from "@mui/icons-material/KeyboardArrowUp";
import { Link as RouterLink } from "react-router-dom";
import { api } from "../api/client";
import ProjectSelect from "../components/ProjectSelect";
import EmptyState from "../components/EmptyState";

interface CoverageCase {
  id: string;
  code: string;
  title: string;
  status: number;
  lastResultStatus?: number | null;
  lastExecutedAt?: string | null;
  openDefectCodes: string[];
}

interface CoverageStory {
  id: string;
  title: string;
  testCaseCount: number;
  passedCount: number;
  failedCount: number;
  coverageStatus: string;
  testCases: CoverageCase[];
}

interface CoverageRequirement {
  id: string;
  code: string;
  title: string;
  moduleName: string;
  storyCount: number;
  coveredStoryCount: number;
  stories: CoverageStory[];
}

interface RequirementsCoverage {
  projectId: string;
  totalRequirements: number;
  totalStories: number;
  coveredStories: number;
  uncoveredStories: number;
  coveragePercent: number;
  requirements: CoverageRequirement[];
}

const STATUS_COLOR: Record<string, "default" | "success" | "error" | "warning" | "info"> = {
  Uncovered: "default",
  Covered: "info",
  Passed: "success",
  Failed: "error",
  NotExecuted: "warning",
};

const RESULT_LABEL: Record<number, string> = {
  1: "Passed", 2: "Failed", 3: "Skipped", 4: "Blocked", 5: "Error",
};

export default function TraceabilityPage() {
  const [projectId, setProjectId] = useState("");
  const [data, setData] = useState<RequirementsCoverage | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [openReq, setOpenReq] = useState<Record<string, boolean>>({});

  useEffect(() => {
    if (!projectId) return;
    setError(null);
    api.get<RequirementsCoverage>(`/projects/${projectId}/traceability/coverage`)
      .then((r) => setData(r.data))
      .catch(() => {
        setData(null);
        setError("No fue posible cargar la matriz de trazabilidad.");
      });
  }, [projectId]);

  return (
    <Box className="flex flex-col gap-4 p-4">
      <Box className="flex flex-wrap items-center justify-between gap-3">
        <Typography variant="h5" fontWeight={700}>Trazabilidad &amp; Cobertura</Typography>
        <Box className="flex gap-2 items-center">
          <Button component={RouterLink} to="/casos" size="small" variant="outlined">
            Vincular casos
          </Button>
          <ProjectSelect value={projectId} onChange={setProjectId} />
        </Box>
      </Box>

      {error && <Alert severity="error">{error}</Alert>}
      {projectId && !error && (
        <Typography variant="body2" color="text.secondary">
          La cobertura se calcula con el vínculo Caso → Historia (desde Casos, icono de enlace).
          Cree módulos, requerimientos e historias en Catálogo si la matriz está vacía.
        </Typography>
      )}

      {data && (
        <Box className="grid grid-cols-2 md:grid-cols-5 gap-3">
          {[
            ["Requerimientos", data.totalRequirements],
            ["Historias", data.totalStories],
            ["Cubiertas", data.coveredStories],
            ["Sin casos", data.uncoveredStories],
            ["Cobertura", `${data.coveragePercent}%`],
          ].map(([label, value]) => (
            <Paper key={label as string} className="p-3">
              <Typography variant="caption" color="text.secondary">{label}</Typography>
              <Typography variant="h5" fontWeight={700}>{value}</Typography>
            </Paper>
          ))}
        </Box>
      )}

      {!data && !error && projectId && (
        <EmptyState variant="loading" title="Cargando cobertura…" description="Consultando requerimientos, historias y resultados." />
      )}

      {data && data.requirements.length === 0 && (
        <EmptyState
          variant="no-data"
          title="Sin requerimientos en el catálogo"
          description="Cree módulos, requerimientos e historias en Catálogo, y vincule casos a historias."
        />
      )}

      {data && data.requirements.length > 0 && (
        <TableContainer component={Paper}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell width={48} />
                <TableCell>Requerimiento</TableCell>
                <TableCell>Módulo</TableCell>
                <TableCell align="right">Historias</TableCell>
                <TableCell align="right">Cubiertas</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {data.requirements.map((req) => {
                const open = !!openReq[req.id];
                return (
                  <Fragment key={req.id}>
                    <TableRow hover>
                      <TableCell>
                        <IconButton size="small" onClick={() => setOpenReq((s) => ({ ...s, [req.id]: !open }))}>
                          {open ? <KeyboardArrowUpIcon /> : <KeyboardArrowDownIcon />}
                        </IconButton>
                      </TableCell>
                      <TableCell>
                        <Typography fontWeight={600}>{req.code}</Typography>
                        <Typography variant="body2" color="text.secondary">{req.title}</Typography>
                      </TableCell>
                      <TableCell>{req.moduleName}</TableCell>
                      <TableCell align="right">{req.storyCount}</TableCell>
                      <TableCell align="right">{req.coveredStoryCount}</TableCell>
                    </TableRow>
                    <TableRow>
                      <TableCell colSpan={5} sx={{ py: 0, border: 0 }}>
                        <Collapse in={open} timeout="auto" unmountOnExit>
                          <Box className="p-3">
                            {req.stories.map((story) => (
                              <Box key={story.id} className="mb-3 p-2 rounded border border-gray-200 dark:border-gray-700">
                                <Box className="flex flex-wrap items-center gap-2 mb-1">
                                  <Typography fontWeight={600}>{story.title}</Typography>
                                  <Chip size="small" label={story.coverageStatus} color={STATUS_COLOR[story.coverageStatus] ?? "default"} />
                                  <Typography variant="caption" color="text.secondary">
                                    {story.testCaseCount} casos · {story.passedCount} OK · {story.failedCount} fail
                                  </Typography>
                                </Box>
                                {story.testCases.length === 0 ? (
                                  <Typography variant="body2" color="text.secondary">Sin casos vinculados.</Typography>
                                ) : (
                                  <Table size="small">
                                    <TableHead>
                                      <TableRow>
                                        <TableCell>Código</TableCell>
                                        <TableCell>Título</TableCell>
                                        <TableCell>Último resultado</TableCell>
                                        <TableCell>Defectos abiertos</TableCell>
                                      </TableRow>
                                    </TableHead>
                                    <TableBody>
                                      {story.testCases.map((tc) => (
                                        <TableRow key={tc.id}>
                                          <TableCell>{tc.code}</TableCell>
                                          <TableCell>{tc.title}</TableCell>
                                          <TableCell>
                                            {tc.lastResultStatus != null
                                              ? RESULT_LABEL[tc.lastResultStatus] ?? String(tc.lastResultStatus)
                                              : "—"}
                                          </TableCell>
                                          <TableCell>
                                            {tc.openDefectCodes.length
                                              ? tc.openDefectCodes.join(", ")
                                              : "—"}
                                          </TableCell>
                                        </TableRow>
                                      ))}
                                    </TableBody>
                                  </Table>
                                )}
                              </Box>
                            ))}
                          </Box>
                        </Collapse>
                      </TableCell>
                    </TableRow>
                  </Fragment>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}
