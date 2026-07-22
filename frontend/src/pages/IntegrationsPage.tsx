import { useEffect, useState } from "react";
import {
  Alert, Box, Button, Card, CardContent, Chip, CircularProgress, FormControl, Grid2 as Grid,
  InputLabel, Link, MenuItem, Select, Table, TableBody, TableCell, TableHead, TableRow,
  TextField, Typography,
} from "@mui/material";
import AutoFixHighIcon from "@mui/icons-material/AutoFixHigh";
import SettingsIcon from "@mui/icons-material/Settings";
import StorageIcon from "@mui/icons-material/Storage";
import { api } from "../api/client";
import ProjectSelect from "../components/ProjectSelect";
import IntegrationConfigDialog from "../components/IntegrationConfigDialog";

interface DbEnvironment {
  id: string;
  projectId: string;
  name: string;
  isActive: boolean;
}

interface DbValidationRun {
  id: string;
  sourceEnvironment: string;
  targetEnvironment: string;
  status: string;
  differencesCount: number;
  startedAt: string;
}

interface SonarMetrics {
  projectKey: string;
  coveragePercent: number;
  duplicationPercent: number;
  bugs: number;
  vulnerabilities: number;
  securityHotspots: number;
  codeSmells: number;
  qualityGateStatus: string;
}

interface PullRequest {
  number: number;
  title: string;
  author: string;
  headBranch: string;
  baseBranch: string;
  url: string;
  createdAt: string;
}

export default function IntegrationsPage() {
  const [projectId, setProjectId] = useState("");
  const [sonar, setSonar] = useState<SonarMetrics | null>(null);
  const [sonarMissing, setSonarMissing] = useState(false);
  const [pulls, setPulls] = useState<PullRequest[] | null>(null);
  const [pullsError, setPullsError] = useState(false);
  const [analyzing, setAnalyzing] = useState<number | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [configOpen, setConfigOpen] = useState(false);
  const [dbEnvs, setDbEnvs] = useState<DbEnvironment[]>([]);
  const [dbRuns, setDbRuns] = useState<DbValidationRun[]>([]);
  const [sourceEnv, setSourceEnv] = useState("");
  const [targetEnv, setTargetEnv] = useState("");
  const [dbStarting, setDbStarting] = useState(false);
  const [newEnvName, setNewEnvName] = useState("");
  const [newEnvConn, setNewEnvConn] = useState("");
  const [savingEnv, setSavingEnv] = useState(false);

  const loadIntegrationData = () => {
    if (!projectId) return;
    setSonar(null); setSonarMissing(false); setPulls(null); setPullsError(false);
    setDbEnvs([]); setDbRuns([]); setSourceEnv(""); setTargetEnv("");

    api.get<SonarMetrics>(`/integrations/sonarqube/${projectId}`)
      .then((r) => setSonar(r.data))
      .catch(() => setSonarMissing(true));
    api.get<PullRequest[]>(`/integrations/github/${projectId}/pulls`)
      .then((r) => setPulls(r.data))
      .catch(() => setPullsError(true));
    api.get<DbEnvironment[]>(`/integrations/database-environments/${projectId}`)
      .then((r) => setDbEnvs(r.data))
      .catch(() => setDbEnvs([]));
    api.get<DbValidationRun[]>(`/integrations/database-validation/${projectId}`)
      .then((r) => setDbRuns(r.data.slice(0, 5)))
      .catch(() => setDbRuns([]));
  };

  useEffect(loadIntegrationData, [projectId]);

  const startDbValidation = async () => {
    if (!projectId || !sourceEnv || !targetEnv) return;
    setDbStarting(true);
    setMessage(null);
    try {
      await api.post("/integrations/database-validation", {
        projectId,
        sourceEnvironment: sourceEnv,
        targetEnvironment: targetEnv,
      });
      setMessage(`Validación de esquema encolada: ${sourceEnv} → ${targetEnv}.`);
      const { data } = await api.get<DbValidationRun[]>(`/integrations/database-validation/${projectId}`);
      setDbRuns(data.slice(0, 5));
    } catch {
      setMessage("No se pudo iniciar la validación. Verifique pertenencia al proyecto y entornos configurados.");
    } finally {
      setDbStarting(false);
    }
  };

  const saveDbEnvironment = async () => {
    if (!projectId || !newEnvName.trim() || !newEnvConn.trim()) return;
    setSavingEnv(true);
    setMessage(null);
    try {
      await api.post("/integrations/database-environments", {
        projectId,
        name: newEnvName.trim(),
        connectionString: newEnvConn.trim(),
      });
      setMessage(`Entorno '${newEnvName.trim()}' guardado (connection string cifrada en servidor).`);
      setNewEnvName("");
      setNewEnvConn("");
      const { data } = await api.get<DbEnvironment[]>(`/integrations/database-environments/${projectId}`);
      setDbEnvs(data);
    } catch {
      setMessage("No se pudo guardar el entorno de BD.");
    } finally {
      setSavingEnv(false);
    }
  };

  const analyzePr = async (prNumber: number) => {
    setAnalyzing(prNumber);
    setMessage(null);
    try {
      const { data } = await api.post<{ summary: string }>(
        `/integrations/github/${projectId}/pulls/${prNumber}/analyze`);
      setMessage(data.summary);
    } catch {
      setMessage("El análisis del PR falló; revise la configuración de GitHub e IA.");
    } finally {
      setAnalyzing(null);
    }
  };

  const importPostman = async () => {
    const input = document.createElement("input");
    input.type = "file";
    input.accept = ".json";
    input.onchange = async () => {
      const file = input.files?.[0];
      if (!file) return;
      try {
        const collectionJson = await file.text();
        const { data } = await api.post<{ testCaseCode: string; requestsImported: number }>(
          "/integrations/postman/import", { projectId, collectionJson });
        setMessage(`Collection importada como ${data.testCaseCode} (${data.requestsImported} requests).`);
      } catch {
        setMessage("No se pudo importar la collection. Verifique que sea un JSON de Postman válido.");
      }
    };
    input.click();
  };

  const generatePipeline = () => {
    void api
      .get(`/integrations/github/${projectId}/pipeline`, { params: { download: true }, responseType: "blob" })
      .then((r) => {
        const url = URL.createObjectURL(r.data as Blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = "qa-guardian-pipeline.yml";
        link.click();
        URL.revokeObjectURL(url);
      })
      .catch(() => setMessage("No se pudo generar el pipeline."));
  };

  const sonarKpis = sonar && [
    { label: "Cobertura", value: `${sonar.coveragePercent}%` },
    { label: "Duplicación", value: `${sonar.duplicationPercent}%` },
    { label: "Bugs", value: sonar.bugs },
    { label: "Vulnerabilidades", value: sonar.vulnerabilities },
    { label: "Security Hotspots", value: sonar.securityHotspots },
    { label: "Code Smells", value: sonar.codeSmells },
  ];

  return (
    <div className="flex flex-col gap-4">
      <Box className="flex items-center justify-between">
        <Typography variant="h5" fontWeight={700}>
          Integraciones
        </Typography>
        <Box className="flex items-center gap-2">
          <Button
            variant="contained"
            size="small"
            startIcon={<SettingsIcon />}
            disabled={!projectId}
            onClick={() => setConfigOpen(true)}
          >
            Configurar Integración
          </Button>
          <Button variant="outlined" size="small" disabled={!projectId} onClick={() => void importPostman()}>
            Importar Postman
          </Button>
          <Button variant="outlined" size="small" disabled={!projectId} onClick={generatePipeline}>
            Generar pipeline
          </Button>
          <ProjectSelect value={projectId} onChange={setProjectId} />
        </Box>
      </Box>

      {message && <Alert severity="info" onClose={() => setMessage(null)}>{message}</Alert>}

      {projectId && (
        <IntegrationConfigDialog
          projectId={projectId}
          open={configOpen}
          onClose={() => setConfigOpen(false)}
          onSuccess={() => {
            setMessage("Integración configurada exitosamente.");
            loadIntegrationData();
          }}
        />
      )}

      <Card>
        <CardContent>
          <Box className="flex items-center gap-3 mb-3">
            <Typography variant="subtitle1" fontWeight={600}>SonarQube</Typography>
            {sonar && (
              <Chip label={`Quality Gate: ${sonar.qualityGateStatus}`} size="small"
                color={sonar.qualityGateStatus === "OK" ? "success" : "error"} />
            )}
          </Box>
          {sonarMissing && (
            <Alert severity="warning">
              SonarQube no está configurado para este proyecto. Use el botón{" "}
              <strong>Configurar Integración</strong> para conectarlo.
            </Alert>
          )}
          {!sonar && !sonarMissing && projectId && <CircularProgress size={24} />}
          {sonarKpis && (
            <Grid container spacing={2}>
              {sonarKpis.map((kpi) => (
                <Grid key={kpi.label} size={{ xs: 6, sm: 4, md: 2 }}>
                  <Box className="rounded-lg bg-guardian-100 p-3 text-center">
                    <Typography variant="h5" fontWeight={700}>{kpi.value}</Typography>
                    <Typography variant="caption">{kpi.label}</Typography>
                  </Box>
                </Grid>
              ))}
            </Grid>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardContent>
          <Box className="flex items-center gap-2 mb-3">
            <StorageIcon fontSize="small" color="primary" />
            <Typography variant="subtitle1" fontWeight={600}>
              Validación de esquema SQL (entornos nombrados)
            </Typography>
          </Box>
          <Alert severity="info" sx={{ mb: 2 }}>
            Las connection strings no se envían desde el navegador al validar. Configure entornos
            (dev, staging, …) en el servidor; la API solo acepta nombres.
          </Alert>
          {!projectId && (
            <Typography variant="body2" color="text.secondary">Seleccione un proyecto.</Typography>
          )}
          {projectId && (
            <>
              <Grid container spacing={2} sx={{ mb: 2 }}>
                <Grid size={{ xs: 12, sm: 4 }}>
                  <TextField
                    size="small" fullWidth label="Nombre entorno" placeholder="dev"
                    value={newEnvName} onChange={(e) => setNewEnvName(e.target.value)}
                  />
                </Grid>
                <Grid size={{ xs: 12, sm: 6 }}>
                  <TextField
                    size="small" fullWidth type="password" label="Connection string (solo al configurar)"
                    value={newEnvConn} onChange={(e) => setNewEnvConn(e.target.value)}
                    helperText="Se cifra en el servidor; no se reutiliza en el POST de validación."
                  />
                </Grid>
                <Grid size={{ xs: 12, sm: 2 }} className="flex items-center">
                  <Button
                    fullWidth variant="outlined" size="small"
                    disabled={savingEnv || !newEnvName.trim() || !newEnvConn.trim()}
                    onClick={() => void saveDbEnvironment()}
                  >
                    {savingEnv ? <CircularProgress size={16} /> : "Guardar"}
                  </Button>
                </Grid>
              </Grid>
              <Grid container spacing={2} alignItems="center" sx={{ mb: 2 }}>
                <Grid size={{ xs: 12, sm: 4 }}>
                  <FormControl size="small" fullWidth>
                    <InputLabel>Origen</InputLabel>
                    <Select
                      label="Origen" value={sourceEnv}
                      onChange={(e) => setSourceEnv(e.target.value)}
                    >
                      {dbEnvs.map((e) => (
                        <MenuItem key={e.id} value={e.name}>{e.name}</MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                </Grid>
                <Grid size={{ xs: 12, sm: 4 }}>
                  <FormControl size="small" fullWidth>
                    <InputLabel>Destino</InputLabel>
                    <Select
                      label="Destino" value={targetEnv}
                      onChange={(e) => setTargetEnv(e.target.value)}
                    >
                      {dbEnvs.map((e) => (
                        <MenuItem key={e.id} value={e.name}>{e.name}</MenuItem>
                      ))}
                    </Select>
                  </FormControl>
                </Grid>
                <Grid size={{ xs: 12, sm: 4 }}>
                  <Button
                    variant="contained" size="small" fullWidth
                    disabled={dbStarting || !sourceEnv || !targetEnv || sourceEnv === targetEnv}
                    onClick={() => void startDbValidation()}
                  >
                    {dbStarting ? <CircularProgress size={16} color="inherit" /> : "Comparar esquemas"}
                  </Button>
                </Grid>
              </Grid>
              {dbEnvs.length === 0 && (
                <Alert severity="warning" sx={{ mb: 2 }}>
                  No hay entornos configurados. Guarde al menos dos (ej. dev y staging).
                </Alert>
              )}
              {dbRuns.length > 0 && (
                <Table size="small">
                  <TableHead>
                    <TableRow>
                      <TableCell>Origen → Destino</TableCell>
                      <TableCell>Estado</TableCell>
                      <TableCell>Diffs</TableCell>
                      <TableCell>Inicio</TableCell>
                    </TableRow>
                  </TableHead>
                  <TableBody>
                    {dbRuns.map((r) => (
                      <TableRow key={r.id}>
                        <TableCell>{r.sourceEnvironment} → {r.targetEnvironment}</TableCell>
                        <TableCell>{r.status}</TableCell>
                        <TableCell>{r.differencesCount}</TableCell>
                        <TableCell>{new Date(r.startedAt).toLocaleString()}</TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              )}
            </>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardContent>
          <Typography variant="subtitle1" fontWeight={600} gutterBottom>
            GitHub — Pull Requests abiertos
          </Typography>
          {pullsError && (
            <Alert severity="warning">
              GitHub no está configurado para este proyecto o el token no es válido.
            </Alert>
          )}
          {pulls && pulls.length === 0 && <Alert severity="info">No hay Pull Requests abiertos.</Alert>}
          {pulls && pulls.length > 0 && (
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>#</TableCell>
                  <TableCell>Título</TableCell>
                  <TableCell>Autor</TableCell>
                  <TableCell>Rama</TableCell>
                  <TableCell align="center">Agente QA</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {pulls.map((pr) => (
                  <TableRow key={pr.number} hover>
                    <TableCell>
                      <Link href={pr.url} target="_blank" rel="noreferrer">#{pr.number}</Link>
                    </TableCell>
                    <TableCell>{pr.title}</TableCell>
                    <TableCell>{pr.author}</TableCell>
                    <TableCell>{pr.headBranch} → {pr.baseBranch}</TableCell>
                    <TableCell align="center">
                      <Button
                        size="small"
                        variant="outlined"
                        startIcon={analyzing === pr.number ? <CircularProgress size={14} /> : <AutoFixHighIcon />}
                        disabled={analyzing !== null}
                        onClick={() => void analyzePr(pr.number)}
                      >
                        Analizar con IA
                      </Button>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
