import { useEffect, useState } from "react";
import {
  Alert, Box, Button, Chip, CircularProgress, Dialog, DialogActions, DialogContent,
  DialogTitle, MenuItem, Stack, TextField, Typography,
} from "@mui/material";
import PlayArrowIcon from "@mui/icons-material/PlayArrow";
import { api } from "../api/client";
import { FRAMEWORKS, TEST_TYPES, type TestCase } from "../types";
import PostmanForm from "./PostmanForm";
import JMeterForm from "./JMeterForm";
import OWASPZAPForm from "./OWASPZAPForm";
import PlaywrightForm from "./PlaywrightForm";
import SeleniumIDEForm from "./SeleniumIDEForm";

interface Props {
  projectId: string;
  testCase?: TestCase | null; // null/undefined = nuevo script
  open: boolean;
  onClose: () => void;
  onSaved: () => void;
}

interface RunItem {
  name: string;
  status: string;
  durationMs: number;
  errorMessage?: string | null;
}

interface RunResult {
  succeeded: boolean;
  framework: string;
  total: number;
  passed: number;
  failed: number;
  items: RunItem[];
  errorMessage?: string | null;
}

interface SavedScript {
  testCaseId: string;
}

const AUTOMATABLE_FRAMEWORKS = [1, 2, 3, 4, 7]; // Playwright, Postman, JMeter, ZAP, Selenium IDE

export default function ScriptEditorDialog({ projectId, testCase, open, onClose, onSaved }: Props) {
  const [name, setName] = useState("");
  const [framework, setFramework] = useState(1);
  const [type, setType] = useState(1);
  const [content, setContent] = useState("");
  const [url, setUrl] = useState("");
  const [busy, setBusy] = useState(false);
  const [running, setRunning] = useState(false);
  const [runResult, setRunResult] = useState<RunResult | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setMessage(null);
    setRunResult(null);
    if (testCase) {
      setName(testCase.title);
      setFramework(testCase.framework || 1);
      setType(testCase.type);
      // Cargar el contenido existente del script.
      api
        .get<{ content: string }>(`/testcases/${testCase.id}/script`)
        .then((r) => setContent(r.data.content))
        .catch(() => setContent(""));
    } else {
      setName("");
      setFramework(1);
      setType(1);
      setContent("");
      setUrl("");
    }
  }, [open, testCase]);

  const record = async () => {
    if (!url) { setMessage("Ingrese la URL a grabar."); return; }
    setBusy(true);
    setMessage(null);
    try {
      const { data } = await api.post<{ content: string; fromCodegen: boolean; note?: string }>(
        "/testcases/record", { projectId, url });
      setContent(data.content);
      setFramework(1); // Playwright
      setMessage(data.fromCodegen ? "Spec grabada con codegen." : data.note ?? "Andamiaje generado.");
    } catch {
      setMessage("No se pudo grabar la spec.");
    } finally {
      setBusy(false);
    }
  };

  // Persiste el script y devuelve el id del caso (para editar o ejecutar). No cierra el diálogo.
  const persist = async (): Promise<string | null> => {
    const { data } = await api.post<SavedScript>("/testcases/script", {
      projectId,
      testCaseId: testCase?.id ?? null,
      name,
      framework,
      type,
      content,
    });
    onSaved();
    return data.testCaseId;
  };

  const save = async () => {
    setBusy(true);
    setMessage(null);
    try {
      await persist();
      onClose();
    } catch (err: unknown) {
      const detail = (err as { response?: { data?: { error?: string } } }).response?.data?.error;
      setMessage(detail ?? "No se pudo guardar el script.");
    } finally {
      setBusy(false);
    }
  };

  // Guarda y ejecuta el caso con el runner de su framework, mostrando el resultado en línea.
  const saveAndRun = async () => {
    setBusy(true);
    setRunning(true);
    setMessage(null);
    setRunResult(null);
    try {
      const id = await persist();
      if (!id) throw new Error("No se obtuvo el identificador del caso guardado.");
      const { data } = await api.post<RunResult>(`/testcases/${id}/run`);
      setRunResult(data);
    } catch (err: unknown) {
      const detail = (err as { response?: { data?: { error?: string } } }).response?.data?.error;
      setMessage(detail ?? "No se pudo ejecutar la prueba.");
    } finally {
      setBusy(false);
      setRunning(false);
    }
  };

  const statusColor = (status: string): "success" | "error" | "warning" | "default" => {
    const s = status.toLowerCase();
    if (s === "passed") return "success";
    if (s === "failed") return "error";
    if (s === "skipped" || s === "flaky") return "warning";
    return "default";
  };

  return (
    <Dialog open={open} onClose={onClose} maxWidth="md" fullWidth>
      <DialogTitle>{testCase ? `Editar script — ${testCase.code}` : "Nuevo script automatizado"}</DialogTitle>
      <DialogContent className="flex flex-col gap-4 pt-2">
        {message && <Alert severity="info" onClose={() => setMessage(null)}>{message}</Alert>}

        <Box className="flex gap-3">
          <TextField label="Nombre" value={name} onChange={(e) => setName(e.target.value)} fullWidth required />
          <TextField select label="Framework" value={framework}
            onChange={(e) => setFramework(Number(e.target.value))} className="min-w-40">
            {AUTOMATABLE_FRAMEWORKS.map((f) => (
              <MenuItem key={f} value={f}>{FRAMEWORKS[f]}</MenuItem>
            ))}
          </TextField>
          <TextField select label="Tipo" value={type}
            onChange={(e) => setType(Number(e.target.value))} className="min-w-40">
            {Object.entries(TEST_TYPES).slice(0, 8).map(([v, label]) => (
              <MenuItem key={v} value={Number(v)}>{label}</MenuItem>
            ))}
          </TextField>
        </Box>

        {/* Formularios específicos por Framework */}
        {framework === 1 && (
          <PlaywrightForm
            content={content}
            onChange={setContent}
            recordUrl={url}
            onRecordUrlChange={setUrl}
            onRecord={() => void record()}
            busy={busy}
          />
        )}

        {framework === 2 && (
          <PostmanForm content={content} onChange={setContent} />
        )}

        {framework === 3 && (
          <JMeterForm content={content} onChange={setContent} />
        )}

        {framework === 4 && (
          <OWASPZAPForm content={content} onChange={setContent} />
        )}

        {framework === 7 && (
          <SeleniumIDEForm content={content} onChange={setContent} />
        )}

        {/* Textarea para otros frameworks (Manual, SQL, etc.) */}
        {framework !== 1 && framework !== 2 && framework !== 3 && framework !== 4 && framework !== 7 && (
          <TextField
            label="Contenido del script"
            value={content}
            onChange={(e) => setContent(e.target.value)}
            multiline
            minRows={16}
            fullWidth
            slotProps={{ input: { style: { fontFamily: "Consolas, 'Courier New', monospace", fontSize: 13 } } }}
          />
        )}

        {/* Resultado de "Guardar y ejecutar" */}
        {running && (
          <Alert severity="info" icon={<CircularProgress size={18} />}>
            Ejecutando la prueba con el runner de {FRAMEWORKS[framework]}…
          </Alert>
        )}

        {runResult && !running && <RunResultPanel result={runResult} statusColor={statusColor} />}
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>Cerrar</Button>
        <Button
          variant="outlined"
          color="success"
          startIcon={<PlayArrowIcon />}
          onClick={() => void saveAndRun()}
          disabled={busy || !name || !content}
        >
          {running ? <CircularProgress size={20} /> : "Guardar y ejecutar"}
        </Button>
        <Button variant="contained" onClick={() => void save()} disabled={busy || !name || !content}>
          {busy && !running ? <CircularProgress size={20} /> : "Guardar"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

/** Panel con el resultado de ejecutar el caso individual. */
function RunResultPanel({
  result,
  statusColor,
}: {
  result: RunResult;
  statusColor: (s: string) => "success" | "error" | "warning" | "default";
}) {
  // Falla de runner (herramienta no disponible, sin reporte, etc.).
  if (!result.succeeded) {
    return (
      <Alert severity="error">
        <Typography variant="body2" sx={{ fontWeight: 600 }}>
          No se pudo ejecutar la prueba
        </Typography>
        <Typography variant="caption" sx={{ whiteSpace: "pre-wrap" }}>
          {result.errorMessage ?? "El motor de ejecución no devolvió resultados."}
        </Typography>
      </Alert>
    );
  }

  // El runner corrió pero no reportó pruebas individuales.
  if (result.total === 0) {
    return (
      <Alert severity="warning">
        {result.errorMessage ?? "El runner no reportó pruebas para este script."}
      </Alert>
    );
  }

  const allPassed = result.failed === 0;

  return (
    <Box sx={{ border: "1px solid #e5e7eb", borderRadius: 1, overflow: "hidden" }}>
      <Box
        sx={{
          px: 2,
          py: 1.25,
          background: allPassed ? "#f0fdf4" : "#fef2f2",
          borderBottom: "1px solid #e5e7eb",
          display: "flex",
          alignItems: "center",
          justifyContent: "space-between",
        }}
      >
        <Typography variant="body2" sx={{ fontWeight: 700, color: allPassed ? "#15803d" : "#b91c1c" }}>
          {allPassed ? "✓ Prueba exitosa" : "✗ Prueba con fallos"}
        </Typography>
        <Typography variant="caption" sx={{ color: "#6b7280" }}>
          {result.passed}/{result.total} exitosas · {result.framework}
        </Typography>
      </Box>
      <Stack divider={<Box sx={{ borderBottom: "1px solid #f3f4f6" }} />}>
        {result.items.map((item, i) => (
          <Box key={i} sx={{ px: 2, py: 1 }}>
            <Box sx={{ display: "flex", alignItems: "center", gap: 1 }}>
              <Chip label={item.status} size="small" color={statusColor(item.status)} sx={{ height: 22 }} />
              <Typography variant="body2" sx={{ flex: 1, fontSize: 13 }}>
                {item.name}
              </Typography>
              <Typography variant="caption" sx={{ color: "#6b7280" }}>
                {item.durationMs}ms
              </Typography>
            </Box>
            {item.errorMessage && (
              <Typography
                variant="caption"
                sx={{
                  display: "block",
                  mt: 0.5,
                  color: "#b91c1c",
                  whiteSpace: "pre-wrap",
                  fontFamily: "monospace",
                  fontSize: 11,
                }}
              >
                {item.errorMessage}
              </Typography>
            )}
          </Box>
        ))}
      </Stack>
    </Box>
  );
}
