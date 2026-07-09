import { useEffect, useState } from "react";
import {
  Alert, Box, Button, Card, CardContent, Chip, CircularProgress, Grid2 as Grid,
  Link, Table, TableBody, TableCell, TableHead, TableRow, Typography,
} from "@mui/material";
import AutoFixHighIcon from "@mui/icons-material/AutoFixHigh";
import { api } from "../api/client";
import ProjectSelect from "../components/ProjectSelect";

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

  useEffect(() => {
    if (!projectId) return;
    setSonar(null); setSonarMissing(false); setPulls(null); setPullsError(false);

    api.get<SonarMetrics>(`/integrations/sonarqube/${projectId}`)
      .then((r) => setSonar(r.data))
      .catch(() => setSonarMissing(true));
    api.get<PullRequest[]>(`/integrations/github/${projectId}/pulls`)
      .then((r) => setPulls(r.data))
      .catch(() => setPullsError(true));
  }, [projectId]);

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
              SonarQube no está configurado para este proyecto. Configure la integración desde la API
              (<code>POST /api/v1/integrations</code>).
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
