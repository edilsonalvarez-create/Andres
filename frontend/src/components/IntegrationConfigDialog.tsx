import { useState } from "react";
import {
  Alert,
  Box,
  Button,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import CheckCircleIcon from "@mui/icons-material/CheckCircle";
import ErrorIcon from "@mui/icons-material/Error";
import ChevronRightIcon from "@mui/icons-material/ChevronRight";
import { api } from "../api/client";

// Coincide con QAGuardian.Domain.Enums.IntegrationType (serializado como número).
type IntegrationTypeId = 1 | 2 | 3 | 4 | 5;

interface IntegrationDef {
  type: IntegrationTypeId;
  label: string;
  description: string;
  requiresToken: boolean;
  requiresBaseUrl: boolean;
  extraFields: { name: string; label: string }[];
}

const INTEGRATIONS: IntegrationDef[] = [
  {
    type: 1, // SonarQube
    label: "SonarQube",
    description: "Análisis de calidad de código: cobertura, duplicación, bugs, hotspots",
    requiresToken: true,
    requiresBaseUrl: true,
    extraFields: [{ name: "projectKey", label: "Clave del Proyecto" }],
  },
  {
    type: 2, // GitHub
    label: "GitHub",
    description: "Pull Requests, checks y análisis automático con IA",
    requiresToken: true,
    requiresBaseUrl: false,
    extraFields: [{ name: "repository", label: "Repositorio (owner/repo)" }],
  },
  {
    type: 3, // OwaspZap
    label: "OWASP ZAP",
    description: "Escaneo de seguridad automático (SQLi, XSS, CSRF, headers)",
    requiresToken: false,
    requiresBaseUrl: true,
    extraFields: [],
  },
  {
    type: 4, // JMeter
    label: "JMeter",
    description: "Pruebas de rendimiento vinculadas como planes .jmx",
    requiresToken: false,
    requiresBaseUrl: true,
    extraFields: [],
  },
  {
    type: 5, // Postman
    label: "Postman",
    description: "Ejecución de collections vía Newman",
    requiresToken: false,
    requiresBaseUrl: true,
    extraFields: [{ name: "postmanEnvironment", label: "Environment (ruta o JSON)" }],
  },
];

interface Props {
  projectId: string;
  open: boolean;
  onClose: () => void;
  onSuccess?: () => void;
}

const GITHUB_BASE_URL = "https://api.github.com";

export default function IntegrationConfigDialog({ projectId, open, onClose, onSuccess }: Props) {
  const [selected, setSelected] = useState<IntegrationDef | null>(null);
  const [baseUrl, setBaseUrl] = useState("");
  const [token, setToken] = useState("");
  const [extra, setExtra] = useState<Record<string, string>>({});
  const [testing, setTesting] = useState(false);
  const [testResult, setTestResult] = useState<{ success: boolean; message: string } | null>(null);
  const [saving, setSaving] = useState(false);

  const buildExtraJson = () => {
    const payload: Record<string, string> = {};
    for (const field of selected?.extraFields ?? []) {
      if (extra[field.name]) payload[field.name] = extra[field.name];
    }
    return Object.keys(payload).length > 0 ? JSON.stringify(payload) : null;
  };

  const effectiveBaseUrl = () => (selected?.type === 2 ? GITHUB_BASE_URL : baseUrl);

  const handleTestConnection = async () => {
    if (!selected) return;
    setTesting(true);
    setTestResult(null);
    try {
      await api.post("/integrations/test-connection", {
        type: selected.type,
        baseUrl: effectiveBaseUrl(),
        token: token || null,
        extraJson: buildExtraJson(),
      });
      setTestResult({ success: true, message: "Conexión exitosa ✓" });
    } catch (error: any) {
      setTestResult({
        success: false,
        message: error.response?.data?.error ?? "No se pudo establecer la conexión",
      });
    } finally {
      setTesting(false);
    }
  };

  const handleSave = async () => {
    if (!selected) return;
    setSaving(true);
    try {
      await api.post("/integrations", {
        projectId,
        type: selected.type,
        baseUrl: effectiveBaseUrl(),
        token: token || null,
        extraJson: buildExtraJson(),
        isEnabled: true,
      });
      resetForm();
      onSuccess?.();
      onClose();
    } catch {
      setTestResult({ success: false, message: "Error guardando la integración" });
    } finally {
      setSaving(false);
    }
  };

  const resetForm = () => {
    setSelected(null);
    setBaseUrl("");
    setToken("");
    setExtra({});
    setTestResult(null);
  };

  const handleClose = () => {
    resetForm();
    onClose();
  };

  const canTest = selected
    ? (!selected.requiresBaseUrl || baseUrl.trim().length > 0)
      && (!selected.requiresToken || token.trim().length > 0)
    : false;

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Configurar Integración</DialogTitle>
      <DialogContent className="flex flex-col gap-4 mt-4">
        {!selected ? (
          <Stack spacing={2}>
            {INTEGRATIONS.map((integration) => (
              <Box
                key={integration.type}
                onClick={() => setSelected(integration)}
                sx={{
                  p: 2,
                  border: "1px solid #ddd",
                  borderRadius: "8px",
                  cursor: "pointer",
                  "&:hover": { backgroundColor: "#f5f5f5" },
                }}
              >
                <Box className="flex justify-between items-center">
                  <div>
                    <Typography variant="subtitle1" fontWeight={700}>{integration.label}</Typography>
                    <Typography variant="body2" color="textSecondary">{integration.description}</Typography>
                  </div>
                  <ChevronRightIcon />
                </Box>
              </Box>
            ))}
          </Stack>
        ) : (
          <Stack spacing={2}>
            <Box className="flex items-center gap-2 mb-2">
              <Button size="small" onClick={() => { setSelected(null); setTestResult(null); }}>
                ← Atrás
              </Button>
              <Typography variant="h6" fontWeight={700}>{selected.label}</Typography>
            </Box>

            {selected.requiresBaseUrl && (
              <TextField
                label={selected.type === 3 ? "URL Objetivo" : "URL Base"}
                value={baseUrl}
                onChange={(e) => setBaseUrl(e.target.value)}
                fullWidth
                required
                placeholder="https://..."
              />
            )}
            {selected.requiresToken && (
              <TextField
                label="Token de Acceso"
                type="password"
                value={token}
                onChange={(e) => setToken(e.target.value)}
                fullWidth
                required
              />
            )}
            {selected.extraFields.map((field) => (
              <TextField
                key={field.name}
                label={field.label}
                value={extra[field.name] ?? ""}
                onChange={(e) => setExtra({ ...extra, [field.name]: e.target.value })}
                fullWidth
              />
            ))}

            {testResult && (
              <Alert
                severity={testResult.success ? "success" : "error"}
                icon={testResult.success ? <CheckCircleIcon /> : <ErrorIcon />}
              >
                {testResult.message}
              </Alert>
            )}

            <Button
              variant="outlined"
              onClick={handleTestConnection}
              disabled={testing || !canTest}
              startIcon={testing ? <CircularProgress size={20} /> : undefined}
            >
              {testing ? "Probando..." : "Probar Conexión"}
            </Button>
          </Stack>
        )}
      </DialogContent>
      <DialogActions>
        <Button onClick={handleClose}>Cancelar</Button>
        {selected && (
          <Button
            variant="contained"
            onClick={handleSave}
            disabled={saving || !testResult?.success}
            startIcon={saving ? <CircularProgress size={20} /> : undefined}
          >
            {saving ? "Guardando..." : "Guardar"}
          </Button>
        )}
      </DialogActions>
    </Dialog>
  );
}
