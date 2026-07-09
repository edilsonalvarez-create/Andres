import { useEffect, useState } from "react";
import {
  Alert, Box, Button, CircularProgress, Dialog, DialogActions, DialogContent,
  DialogTitle, MenuItem, TextField,
} from "@mui/material";
import FiberManualRecordIcon from "@mui/icons-material/FiberManualRecord";
import { api } from "../api/client";
import { FRAMEWORKS, TEST_TYPES, type TestCase } from "../types";

interface Props {
  projectId: string;
  testCase?: TestCase | null; // null/undefined = nuevo script
  open: boolean;
  onClose: () => void;
  onSaved: () => void;
}

const AUTOMATABLE_FRAMEWORKS = [1, 2, 3, 4]; // Playwright, Postman, JMeter, ZAP

export default function ScriptEditorDialog({ projectId, testCase, open, onClose, onSaved }: Props) {
  const [name, setName] = useState("");
  const [framework, setFramework] = useState(1);
  const [type, setType] = useState(1);
  const [content, setContent] = useState("");
  const [url, setUrl] = useState("");
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  useEffect(() => {
    if (!open) return;
    setMessage(null);
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

  const save = async () => {
    setBusy(true);
    setMessage(null);
    try {
      await api.post("/testcases/script", {
        projectId,
        testCaseId: testCase?.id ?? null,
        name,
        framework,
        type,
        content,
      });
      onSaved();
      onClose();
    } catch (err: unknown) {
      const detail = (err as { response?: { data?: { error?: string } } }).response?.data?.error;
      setMessage(detail ?? "No se pudo guardar el script.");
    } finally {
      setBusy(false);
    }
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

        {framework === 1 && (
          <Box className="flex gap-3 items-center">
            <TextField label="URL a grabar (codegen)" value={url}
              onChange={(e) => setUrl(e.target.value)} size="small" className="flex-1" />
            <Button variant="outlined" color="error" startIcon={<FiberManualRecordIcon />}
              disabled={busy} onClick={() => void record()}>
              Grabar
            </Button>
          </Box>
        )}

        <TextField
          label="Contenido del script"
          value={content}
          onChange={(e) => setContent(e.target.value)}
          multiline
          minRows={16}
          fullWidth
          slotProps={{ input: { style: { fontFamily: "Consolas, 'Courier New', monospace", fontSize: 13 } } }}
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose} disabled={busy}>Cancelar</Button>
        <Button variant="contained" onClick={() => void save()} disabled={busy || !name || !content}>
          {busy ? <CircularProgress size={20} /> : "Guardar"}
        </Button>
      </DialogActions>
    </Dialog>
  );
}
