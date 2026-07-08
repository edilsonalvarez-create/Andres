import { useEffect, useState } from "react";
import { Alert, Card, CardContent, CircularProgress, Grid2 as Grid, Typography } from "@mui/material";
import {
  Bar, BarChart, CartesianGrid, Legend, Line, LineChart,
  ResponsiveContainer, Tooltip, XAxis, YAxis,
} from "recharts";
import { api } from "../api/client";
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

  useEffect(() => {
    api
      .get<DashboardStats>("/dashboard")
      .then((r) => setStats(r.data))
      .catch(() => setError("No fue posible cargar los indicadores."));
  }, []);

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
    { label: "Índice de calidad", value: stats.qualityScore },
  ];

  return (
    <div className="flex flex-col gap-6">
      <Typography variant="h5" fontWeight={700}>
        Dashboard ejecutivo
      </Typography>

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
    </div>
  );
}
