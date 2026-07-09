import { useCallback, useEffect, useState } from "react";
import {
  Alert, Box, Button, Card, CardContent, CircularProgress, Grid2 as Grid, TextField, Typography,
} from "@mui/material";
import DownloadIcon from "@mui/icons-material/Download";
import {
  Bar, BarChart, CartesianGrid, Legend, Line, LineChart,
  ResponsiveContainer, Tooltip, XAxis, YAxis,
} from "recharts";
import { api } from "../api/client";
import FailureHeatmap from "../components/FailureHeatmap";
import type { DashboardStats } from "../types";

function Kpi({ label, value, accent }: { label: string; value: string | number; accent?: string }) {
  return (
    <Card className="h-full">
      <CardContent>
        <Typography variant="body2" color="text.secondary">
          {label}
        </Typography>
        <Typography variant="h4" fontWeight={700} sx={{ color: accent ?? "text.primary" }}>
          {value}
        </Typography>
      </CardContent>
    </Card>
  );
}

export default function DashboardPage() {
  const [stats, setStats] = useState<DashboardStats | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");

  const load = useCallback(() => {
    api
      .get<DashboardStats>("/dashboard", { params: { from: from || undefined, to: to || undefined } })
      .then((r) => setStats(r.data))
      .catch(() => setError("No fue posible cargar los indicadores."));
  }, [from, to]);

  useEffect(() => { load(); }, [load]);

  if (error) return <Alert severity="error">{error}</Alert>;
  if (!stats) return <CircularProgress />;

  const kpis = [
    { label: "Proyectos activos", value: stats.totalProjects },
    { label: "Casos de prueba", value: stats.totalTestCases },
    { label: "Cobertura de automatización", value: `${stats.automationCoveragePercent}%` },
    { label: "Ejecuciones (30 días)", value: stats.runsLast30Days },
    { label: "Pruebas ejecutadas", value: stats.testsExecuted },
    { label: "% de éxito", value: `${stats.passRatePercent}%`, accent: stats.passRatePercent >= 95 ? "#2e7d32" : "#c62828" },
    { label: "Defectos abiertos", value: stats.openDefects },
    { label: "Defectos críticos", value: stats.criticalDefectsOpen, accent: stats.criticalDefectsOpen > 0 ? "#c62828" : "#2e7d32" },
    { label: "Vulnerabilidades altas/críticas", value: stats.vulnerabilitiesHighOrCritical, accent: stats.vulnerabilitiesHighOrCritical > 0 ? "#c62828" : "#2e7d32" },
    { label: "Disponibilidad", value: `${stats.availabilityPercent}%`, accent: stats.availabilityPercent >= 95 ? "#2e7d32" : "#c62828" },
    { label: "Índice de calidad", value: stats.qualityScore },
  ];

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

  return (
    <div className="flex flex-col gap-6">
      <Box className="flex items-center justify-between">
        <Typography variant="h5" fontWeight={700}>
          Dashboard ejecutivo
        </Typography>
        <Box className="flex items-center gap-2">
          <TextField type="date" size="small" label="Desde" value={from}
            onChange={(e) => setFrom(e.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
          <TextField type="date" size="small" label="Hasta" value={to}
            onChange={(e) => setTo(e.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
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

      <Grid container spacing={2}>
        {kpis.map((kpi) => (
          <Grid key={kpi.label} size={{ xs: 6, sm: 4, md: 2.4 }}>
            <Kpi {...kpi} />
          </Grid>
        ))}
      </Grid>

      <Grid container spacing={2}>
        <Grid size={{ xs: 12, md: 7 }}>
          <Card>
            <CardContent>
              <Typography variant="subtitle1" fontWeight={600} gutterBottom>
                Tendencia de ejecuciones (30 días)
              </Typography>
              <ResponsiveContainer width="100%" height={280}>
                <LineChart data={stats.trend.map((t) => ({ ...t, date: t.date.slice(0, 10) }))}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="date" fontSize={12} />
                  <YAxis fontSize={12} />
                  <Tooltip />
                  <Legend />
                  <Line type="monotone" dataKey="passed" name="Exitosas" stroke="#2e7d32" strokeWidth={2} />
                  <Line type="monotone" dataKey="failed" name="Fallidas" stroke="#c62828" strokeWidth={2} />
                </LineChart>
              </ResponsiveContainer>
            </CardContent>
          </Card>
        </Grid>
        <Grid size={{ xs: 12, md: 5 }}>
          <Card>
            <CardContent>
              <Typography variant="subtitle1" fontWeight={600} gutterBottom>
                Errores por módulo
              </Typography>
              <ResponsiveContainer width="100%" height={280}>
                <BarChart data={stats.errorsByModule}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="moduleName" fontSize={12} />
                  <YAxis fontSize={12} allowDecimals={false} />
                  <Tooltip />
                  <Bar dataKey="failedCount" name="Fallos" fill="#3f51b5" />
                </BarChart>
              </ResponsiveContainer>
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
