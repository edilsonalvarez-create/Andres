import { Box, TextField, Typography, Stack, Checkbox } from "@mui/material";
import { useParsedYamlConfig } from "../hooks/useParsedYamlConfig";

interface OWASPZAPFormProps {
  content: string;
  onChange: (content: string) => void;
}

interface OWASPZAPConfig {
  target_url: string;
  scan_type: "quick" | "baseline" | "full";
  intensity: "low" | "medium" | "high";
  checks: string[];
}

const AVAILABLE_CHECKS = [
  "sql_injection",
  "xss",
  "csrf",
  "missing_headers",
  "weak_ssl",
  "auth_bypass",
  "privilege_escalation",
  "path_traversal",
  "command_injection",
  "xxe_injection",
  "clickjacking",
  "cors_misconfiguration",
];

const CHECK_DESCRIPTIONS: Record<string, string> = {
  sql_injection: "Inyección SQL",
  xss: "Cross-Site Scripting (XSS)",
  csrf: "Cross-Site Request Forgery",
  missing_headers: "Headers de Seguridad Faltantes",
  weak_ssl: "Certificados SSL Débiles",
  auth_bypass: "Bypass de Autenticación",
  privilege_escalation: "Escalamiento de Privilegios",
  path_traversal: "Traversal de Directorios",
  command_injection: "Inyección de Comandos",
  xxe_injection: "XML External Entity (XXE)",
  clickjacking: "Clickjacking",
  cors_misconfiguration: "CORS Mal Configurado",
};

export default function OWASPZAPForm({ content, onChange }: OWASPZAPFormProps) {
  const [config, updateConfig] = useParsedYamlConfig(
    content, onChange, defaultConfig, parseYaml, generateYaml
  );

  const handleToggleCheck = (check: string) => {
    const newChecks = config.checks.includes(check)
      ? config.checks.filter((c) => c !== check)
      : [...config.checks, check];
    updateConfig({ ...config, checks: newChecks });
  };

  const scanTypeInfo = {
    quick: { time: "2-5 min", desc: "Escaneo rápido, tests básicos" },
    baseline: { time: "15-30 min", desc: "Escaneo estándar, recomendado" },
    full: { time: "1-2 horas", desc: "Escaneo completo, todas las pruebas" },
  };

  const intensityInfo = {
    low: { desc: "Evita cargar el servidor" },
    medium: { desc: "Balance entre cobertura y carga (recomendado)" },
    high: { desc: "Máxima cobertura, puede sobrecargar" },
  };

  return (
    <Stack spacing={3}>
      {/* URL Target */}
      <Box>
        <TextField
          label="URL del Servidor"
          value={config.target_url}
          onChange={(e) => updateConfig({ ...config, target_url: e.target.value })}
          placeholder="http://localhost:5000"
          fullWidth
          size="small"
          helperText="Solo la URL raíz (sin /api). El scanner escanea todo el servidor."
        />
      </Box>

      {/* Tipo de Escaneo */}
      <Box sx={{ p: 2, background: "#f0fdf4", borderRadius: 1, border: "1px solid #bbf7d0" }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 2 }}>Tipo de Escaneo</Typography>
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 2 }}>
          {(["quick", "baseline", "full"] as const).map((type) => (
            <Box
              key={type}
              onClick={() => updateConfig({ ...config, scan_type: type })}
              sx={{
                p: 2,
                border: `2px solid ${config.scan_type === type ? "#16a34a" : "#e5e7eb"}`,
                borderRadius: 1,
                cursor: "pointer",
                background: config.scan_type === type ? "#f0fdf4" : "#fafafa",
                transition: "all 200ms ease",
                "&:hover": { borderColor: "#16a34a" },
              }}
            >
              <Typography variant="caption" sx={{ fontWeight: 600, color: "#15803d" }}>
                {type.toUpperCase()}
              </Typography>
              <Typography variant="caption" sx={{ display: "block", color: "#6b7280", mt: 0.5 }}>
                ⏱ {scanTypeInfo[type].time}
              </Typography>
              <Typography variant="caption" sx={{ display: "block", color: "#6b7280", mt: 0.5 }}>
                {scanTypeInfo[type].desc}
              </Typography>
            </Box>
          ))}
        </Box>
      </Box>

      {/* Intensidad */}
      <Box sx={{ p: 2, background: "#fef3c7", borderRadius: 1, border: "1px solid #fcd34d" }}>
        <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 2 }}>Intensidad</Typography>
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(3, 1fr)", gap: 2 }}>
          {(["low", "medium", "high"] as const).map((intensity) => (
            <Box
              key={intensity}
              onClick={() => updateConfig({ ...config, intensity })}
              sx={{
                p: 2,
                border: `2px solid ${config.intensity === intensity ? "#f59e0b" : "#e5e7eb"}`,
                borderRadius: 1,
                cursor: "pointer",
                background: config.intensity === intensity ? "#fffbeb" : "#fafafa",
                transition: "all 200ms ease",
                "&:hover": { borderColor: "#f59e0b" },
              }}
            >
              <Typography variant="caption" sx={{ fontWeight: 600, color: "#b45309" }}>
                {intensity.toUpperCase()}
              </Typography>
              <Typography variant="caption" sx={{ display: "block", color: "#6b7280", mt: 0.5 }}>
                {intensityInfo[intensity].desc}
              </Typography>
            </Box>
          ))}
        </Box>
      </Box>

      {/* Checks (Vulnerabilidades a buscar) */}
      <Box>
        <Typography variant="subtitle2" sx={{ fontWeight: 600, mb: 2 }}>Vulnerabilidades a Buscar</Typography>
        <Box sx={{ display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(200px, 1fr))", gap: 1 }}>
          {AVAILABLE_CHECKS.map((check) => (
            <Box
              key={check}
              onClick={() => handleToggleCheck(check)}
              sx={{
                p: 1.5,
                border: `1px solid ${config.checks.includes(check) ? "#2563eb" : "#e5e7eb"}`,
                borderRadius: 1,
                cursor: "pointer",
                background: config.checks.includes(check) ? "#eff6ff" : "#fafafa",
                transition: "all 150ms ease",
                "&:hover": { borderColor: "#2563eb", background: "#f0f9ff" },
              }}
            >
              <Box sx={{ display: "flex", alignItems: "flex-start", gap: 1 }}>
                <Checkbox
                  checked={config.checks.includes(check)}
                  onChange={() => handleToggleCheck(check)}
                  size="small"
                  sx={{ p: 0, mt: -0.5 }}
                />
                <Box sx={{ flex: 1 }}>
                  <Typography variant="caption" sx={{ fontWeight: 600, display: "block", color: "#1f2937" }}>
                    {CHECK_DESCRIPTIONS[check]}
                  </Typography>
                  <Typography variant="caption" sx={{ color: "#6b7280", display: "block" }}>
                    {check}
                  </Typography>
                </Box>
              </Box>
            </Box>
          ))}
        </Box>
      </Box>

      {/* Vista previa */}
      <Box sx={{ p: 2, background: "#f9fafb", borderRadius: 1, border: "1px solid #e5e7eb" }}>
        <Typography variant="caption" sx={{ fontWeight: 600, display: "block", mb: 1 }}>Preview YAML:</Typography>
        <pre style={{ fontSize: 11, margin: 0, overflow: "auto", maxHeight: 200, color: "#4b5563" }}>
          {generateYaml(config)}
        </pre>
      </Box>

      {/* Advertencia */}
      <Box sx={{ p: 2, background: "#fef2f2", borderRadius: 1, border: "1px solid #fecaca" }}>
        <Typography variant="caption" sx={{ fontWeight: 600, display: "block", mb: 1, color: "#991b1b" }}>
          ⚠️ Importante
        </Typography>
        <Typography variant="caption" sx={{ display: "block", color: "#7f1d1d" }}>
          • NO ejecutes en PRODUCCIÓN sin permiso<br/>
          • Usa servidores de prueba (staging, dev)<br/>
          • Los escaneos pueden afectar el rendimiento del servidor<br/>
          • Revisa todas las vulnerabilidades encontradas
        </Typography>
      </Box>
    </Stack>
  );
}

function defaultConfig(): OWASPZAPConfig {
  return {
    target_url: "http://localhost:5000",
    scan_type: "baseline",
    intensity: "medium",
    checks: ["sql_injection", "xss", "csrf", "missing_headers"],
  };
}

function parseYaml(content: string): OWASPZAPConfig {
  const urlMatch = content.match(/target_url:\s*(.+)/);
  const typeMatch = content.match(/scan_type:\s*"?(\w+)"?/);
  const intensityMatch = content.match(/intensity:\s*"?(\w+)"?/);

  return {
    target_url: urlMatch ? urlMatch[1].trim() : "http://localhost:5000",
    scan_type: (typeMatch?.[1] as "quick" | "baseline" | "full") || "baseline",
    intensity: (intensityMatch?.[1] as "low" | "medium" | "high") || "medium",
    checks: [],
  };
}

function generateYaml(config: OWASPZAPConfig): string {
  let yaml = `scan:
  name: "OWASP ZAP Scan"
  target_url: ${config.target_url}
  scan_type: "${config.scan_type}"
  intensity: "${config.intensity}"`;

  if (config.checks.length > 0) {
    yaml += "\n  checks:";
    config.checks.forEach((check) => {
      yaml += `\n    - ${check}`;
    });
  }

  yaml += "\n  report_format: json";

  return yaml;
}
