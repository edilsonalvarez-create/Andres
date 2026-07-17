import { useEffect, useState } from "react";
import {
  Alert, Box, Chip, CircularProgress, List, ListItem, ListItemText, Paper, Stack, Typography,
} from "@mui/material";
import PsychologyIcon from "@mui/icons-material/Psychology";
import { api } from "../api/client";
import EmptyState from "./EmptyState";
import type { AiAnalysisItem } from "../types";

interface Props {
  testRunId: string;
}

const RISK: Record<number, string> = {
  0: "Informativo", 1: "Baja", 2: "Media", 3: "Alta", 4: "Crítica",
};

/**
 * Diagnóstico IA persistido de una ejecución (GET /testruns/{id}/ai-analysis).
 * No invoca el LLM: solo muestra lo guardado tras AnalyzeFailuresWithAiAsync.
 */
export default function AiDiagnosisPanel({ testRunId }: Props) {
  const [items, setItems] = useState<AiAnalysisItem[] | null>(null);
  const [error, setError] = useState(false);

  const load = () => {
    setItems(null);
    setError(false);
    api
      .get<AiAnalysisItem[]>(`/testruns/${testRunId}/ai-analysis`)
      .then((r) => setItems(r.data))
      .catch(() => setError(true));
  };

  useEffect(() => { load(); }, [testRunId]);

  return (
    <Paper variant="outlined" sx={{ p: 2 }}>
      <Box className="flex items-center gap-2 mb-2">
        <PsychologyIcon color="primary" />
        <Typography variant="h6" fontWeight={700}>Diagnóstico IA</Typography>
      </Box>

      {error && (
        <EmptyState
          variant="error"
          title="No se pudo cargar el diagnóstico"
          description="Compruebe permisos del proyecto e intente de nuevo."
          onRetry={load}
        />
      )}

      {!error && items === null && (
        <Box className="flex justify-center py-6">
          <CircularProgress size={28} />
        </Box>
      )}

      {!error && items && items.length === 0 && (
        <EmptyState
          variant="no-data"
          title="Sin análisis para esta ejecución"
          description="El diagnóstico aparece cuando la corrida tiene fallos y el motor IA (o heurístico) ya persistió el resultado. No se genera al abrir este panel."
        />
      )}

      {!error && items && items.length > 0 && (
        <Stack spacing={2}>
          {items.map((a) => (
            <Paper key={a.id} variant="outlined" sx={{ p: 2, bgcolor: "action.hover" }}>
              <Box className="flex flex-wrap items-center gap-2 mb-1">
                <Typography fontWeight={600}>
                  {a.testName ?? "Fallo de prueba"}
                </Typography>
                <Chip size="small" label={RISK[a.criticality] ?? a.criticality} color={a.criticality >= 3 ? "error" : "default"} />
                {a.confidence != null && (
                  <Chip size="small" variant="outlined"
                    label={`Confianza ${(a.confidence * 100).toFixed(0)}%`} />
                )}
                <Typography variant="caption" color="text.secondary">{a.modelUsed}</Typography>
              </Box>

              <Typography variant="subtitle2" color="text.secondary">Resumen</Typography>
              <Typography variant="body2" className="mb-2">{a.summary}</Typography>

              <Typography variant="subtitle2" color="text.secondary">Causa probable</Typography>
              <Typography variant="body2" className="mb-2">{a.probableCause}</Typography>

              {a.evidenceQuote && (
                <>
                  <Typography variant="subtitle2" color="text.secondary">Evidencia</Typography>
                  <Alert severity="info" sx={{ mb: 2, py: 0.5 }}>
                    <Typography variant="body2" component="blockquote" sx={{ m: 0, fontStyle: "italic" }}>
                      {a.evidenceQuote}
                    </Typography>
                  </Alert>
                </>
              )}

              <Typography variant="subtitle2" color="text.secondary">Recomendaciones</Typography>
              {a.recommendations.length === 0 ? (
                <Typography variant="body2" color="text.secondary">—</Typography>
              ) : (
                <List dense disablePadding>
                  {a.recommendations.map((rec, i) => (
                    <ListItem key={i} disableGutters sx={{ py: 0.25 }}>
                      <ListItemText primary={rec} primaryTypographyProps={{ variant: "body2" }} />
                    </ListItem>
                  ))}
                </List>
              )}
            </Paper>
          ))}
        </Stack>
      )}
    </Paper>
  );
}
