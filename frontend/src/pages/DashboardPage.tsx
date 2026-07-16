import { useCallback, useEffect, useState } from "react";
import {
  Box, Button, Card, CardContent, Chip, Grid2 as Grid, TextField, Typography, useTheme,
} from "@mui/material";
import DownloadIcon from "@mui/icons-material/Download";
import {
  Bar, BarChart, CartesianGrid, Legend, Line, LineChart,
  ResponsiveContainer, Tooltip, XAxis, YAxis,
} from "recharts";
import { api } from "../api/client";
import FailureHeatmap from "../components/FailureHeatmap";
import EmptyState from "../components/EmptyState";
import ProjectSelect from "../components/ProjectSelect";
import type { DashboardStats } from "../types";

function Kpi({ label, value, color }: { label: string; value: string | number; color?: string }) {
  return (
    <Card className="h-full">
      <CardContent>
        <Typography variant="body2" color="text.secondary">
          {label}
        </Typography>
        <Typography variant="h4" fontWeight={700} sx={{ color: color ?? "text.primary" }}>
          {value}
        </Typography>
      </CardContent>
    </Card>
  );
}

function KpiGroup({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <Box className="flex flex-col gap-2">
      <Typography variant="overline" color="text.secondary" fontWeight={600}>
        {title}
      </Typography>
      <Grid container spacing={2}>
        {children}
      </Grid>
    </Box>
  );
}

const DATE_PRESETS = [
  { label: "7 días", days: 7 },
  { label: "30 días", days: 30 },
  { label: "90 días", days: 90 },
];

function isoDate(d: Date) {
  return d.toISOString().slice(0, 10);
}

export default function DashboardPage() {
  const theme = useTheme();
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [loadError, setLoadError] = useState(false);
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [projectId, setProjectId] = useState("");
  const [activePreset, setActivePreset] = useState<number | null>(null);

  const load = useCallback(() => {
    setLoadError(false);
    api
      .get<DashboardStats>("/dashboard", {
        params: {
          from: from || undefined,
          to: to || undefined,
          projectId: projectId || undefined,
        },
      })
      .then((r) => setStats(r.data))
      .catch(() => setLoadError(true));
  }, [from, to, projectId]);

  useEffect(() => { load(); }, [load]);

  const applyPreset = (days: number) => {
    const end = new Date();
    const start = new Date();
    start.setDate(end.getDate() - days);
    setFrom(isoDate(start));
    setTo(isoDate(end));
    setActivePreset(days);
  };

  const good = theme.palette.success.main;
  const bad = theme.palette.error.main;

  const exportReport = (format: string) => {
    void api
      .get("/dashboard/report", {
        params: { format, from: from || undefined, to: to || undefined },
        responseType: "blob",
      })
      .then((r) => {
        const url = URL.createObjectURL(r.data as Blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = `dashboard.${format.toLowerCase() === "excel" ? "xlsx" : "pdf"}`;
        link.click();
        URL.revokeObjectURL(url);
      });
  };

  if (loadError) {
    return <EmptyState variant="error" onRetry={load} description="No fue posible cargar los indicadores." />;
  }
  if (!stats) {
    return <EmptyState variant="loading" title="Cargando indicadores…" />;
  }

  const hasRisk = stats.criticalDefectsOpen > 0 || stats.vulnerabilitiesHighOrCritical > 0;

  return (
    <div className="flex flex-col gap-6">
      <Box className="flex items-center justify-between flex-wrap gap-2">
        <Typography variant="h5" fontWeight={700}>
          Dashboard ejecutivo
        </Typography>
        <Box className="flex items-center gap-2 flex-wrap">
          <ProjectSelect value={projectId} onChange={setProjectId} />
          {DATE_PRESETS.map((p) => (
            <Chip
              key={p.days}
              label={p.label}
              size="small"
              color={activePreset === p.days ? "primary" : "default"}
              variant={activePreset === p.days ? "filled" : "outlined"}
              onClick={() => applyPreset(p.days)}
              clickable
            />
          ))}
          <TextField type="date" size="small" label="Desde" value={from}
            onChange={(e) => { setFrom(e.target.value); setActivePreset(null); }}
            slotProps={{ inputLabel: { shrink: true } }} />
          <TextField type="date" size="small" label="Hasta" value={to}
            onChange={(e) => { setTo(e.target.value); setActivePreset(null); }}
            slotProps={{ inputLabel: { shrink: true } }} />
          <Button variant="outlined" size="small" startIcon={<DownloadIcon />}
            onClick={() => exportReport("Pdf")}>
            PDF
          </Button>
          <Button variant="outlined" size="small" startIcon={<DownloadIcon />}
            onClick={() => exportReport("Excel")}>
            Excel
          </Button>
        </Box>
      </Box>

      {hasRisk && (
        <Box
          className="flex items-center gap-2 px-4 py-2 rounded-lg"
          sx={{ backgroundColor: "error.main", color: "error.contrastText" }}
          role="status"
        >
          <Typography variant="body2" fontWeight={600}>
            Atención: {stats.criticalDefectsOpen} defecto(s) crítico(s) abierto(s) y{" "}
            {stats.vulnerabilitiesHighOrCritical} vulnerabilidad(es) alta(s)/crítica(s) sin resolver.
          </Typography>
        </Box>
      )}

      <KpiGroup title="Calidad">
        <Grid size={{ xs: 6, sm: 4, md: 2.4 }}><Kpi label="Casos de prueba" value={stats.totalTestCases} /></Grid>
        <Grid size={{ xs: 6, sm: 4, md: 2.4 }}><Kpi label="Cobertura de automatización" value={`${stats.automationCoveragePercent}%`} /></Grid>
        <Grid size={{ xs: 6, sm: 4, md: 2.4 }}><Kpi label="Pruebas ejecutadas" value={stats.testsExecuted} /></Grid>
        <Grid size={{ xs: 6, sm: 4, md: 2.4 }}>
          <Kpi label="% de éxito" value={`${stats.passRatePercent}%`} color={stats.passRatePercent >= 95 ? good : bad} />
        </Grid>
        <Grid size={{ xs: 6, sm: 4, md: 2.4 }}><Kpi label="Índice de calidad" value={stats.qualityScore} /></Grid>
      </KpiGroup>

      <KpiGroup title="Riesgo y seguridad">
        <Grid size={{ xs: 6, sm: 4, md: 3 }}><Kpi label="Defectos abiertos" value={stats.openDefects} /></Grid>
        <Grid size={{ xs: 6, sm: 4, md: 3 }}>
          <Kpi label="Defectos críticos" value={stats.criticalDefectsOpen}
            color={stats.criticalDefectsOpen > 0 ? bad : good} />
        </Grid>
        <Grid size={{ xs: 6, sm: 4, md: 3 }}>
          <Kpi label="Vulnerabilidades altas/críticas" value={stats.vulnerabilitiesHighOrCritical}
            color={stats.vulnerabilitiesHighOrCritical > 0 ? bad : good} />
        </Grid>
        <Grid size={{ xs: 6, sm: 4, md: 3 }}>
          <Kpi label="Disponibilidad" value={`${stats.availabilityPercent}%`}
            color={stats.availabilityPercent >= 95 ? good : bad} />
        </Grid>
      </KpiGroup>

      <KpiGroup title="Entrega">
        <Grid size={{ xs: 6, sm: 4, md: 3 }}><Kpi label="Proyectos activos" value={stats.totalProjects} /></Grid>
        <Grid size={{ xs: 6, sm: 4, md: 3 }}><Kpi label="Ejecuciones (rango seleccionado)" value={stats.runsLast30Days} /></Grid>
      </KpiGroup>

      <Grid container spacing={2}>
        <Grid size={{ xs: 12, md: 7 }}>
          <Card>
            <CardContent>
              <Typography variant="subtitle1" fontWeight={600} gutterBottom>
                Tendencia de ejecuciones
              </Typography>
              <div
                role="img"
                aria-label={`Tendencia de ejecuciones: ${stats.trend.reduce((s, t) => s + t.passed, 0)} exitosas y ${stats.trend.reduce((s, t) => s + t.failed, 0)} fallidas en el rango seleccionado.`}
              >
                <ResponsiveContainer width="100%" height={280}>
                  <LineChart data={stats.trend.map((t) => ({ ...t, date: t.date.slice(0, 10) }))}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="date" fontSize={12} />
                    <YAxis fontSize={12} />
                    <Tooltip />
                    <Legend />
                    <Line type="monotone" dataKey="passed" name="Exitosas" stroke={good} strokeWidth={2} />
                    <Line type="monotone" dataKey="failed" name="Fallidas" stroke={bad} strokeWidth={2} />
                  </LineChart>
                </ResponsiveContainer>
              </div>
            </CardContent>
          </Card>
        </Grid>
        <Grid size={{ xs: 12, md: 5 }}>
          <Card>
            <CardContent>
              <Typography variant="subtitle1" fontWeight={600} gutterBottom>
                Errores por módulo
              </Typography>
              <div
                role="img"
                aria-label={`Errores por módulo: ${stats.errorsByModule.map((m) => `${m.moduleName} con ${m.failedCount} fallos`).join(", ") || "sin errores registrados"}.`}
              >
                <ResponsiveContainer width="100%" height={280}>
                  <BarChart data={stats.errorsByModule}>
                    <CartesianGrid strokeDasharray="3 3" />
                    <XAxis dataKey="moduleName" fontSize={12} />
                    <YAxis fontSize={12} allowDecimals={false} />
                    <Tooltip />
                    <Bar dataKey="failedCount" name="Fallos" fill={theme.palette.secondary.main} />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </CardContent>
          </Card>
        </Grid>
      </Grid>

      <Card>
        <CardContent>
          <Typography variant="subtitle1" fontWeight={600} gutterBottom>
            Heatmap de fallos (módulo × día)
          </Typography>
          <FailureHeatmap cells={stats.heatmap} />
        </CardContent>
      </Card>
    </div>
  );
}
